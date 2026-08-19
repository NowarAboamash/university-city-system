# API Contracts

OpenAPI 3 specs for each backend service, exported from their live `/swagger/v1/swagger.json` endpoints. The dashboard and mobile app teams can feed these into a codegen tool (e.g. `openapi-generator`, `NSwag`, `orval`) to produce typed HTTP clients instead of hand-writing request/response models.

All requests should go through the Gateway (`http://localhost:5067` locally), using the same paths as in each spec below — Ocelot forwards paths unchanged.

| File | Service | Base path (via Gateway) |
|---|---|---|
| `feedback-service.openapi.json` | FeedbackService | `/api/feedbacks`, `/api/feedbackimages` |
| `advertising-service.openapi.json` | AdvertisingService | `/api/ads`, `/api/ad-types`, `/api/target-genders` |
| `notification-service.openapi.json` | NotificationService | `/api/notifications`, `/api/device-tokens` |

### Images are Cloudinary URLs, not local paths

`FeedbackImageDto.imagePath` and `AdvertisementDto.imageUrl` are now full, absolute Cloudinary URLs (e.g. `https://res.cloudinary.com/<cloud>/image/upload/v.../feedback/xyz.png`), publicly fetchable directly — **do not** prefix them with the Gateway or service origin, and don't route them through `/api/...`. Both services used to store images on local disk and serve them via `/uploads/...` or a local proxy endpoint; that's gone (the old `/api/advertisementimages/advertisements/{fileName}` endpoint was removed). This also means images now work correctly across independently-hosted services — no gateway routing needed for them at all.

Rows created before this migration may still hold old local-style paths; those won't resolve unless the originating service is still running with its old `wwwroot/uploads` content.

### Pagination

`GET /api/feedbacks` is paginated via query string, not a request body (GET requests with bodies aren't reliably supported by HTTP tooling/clients):

```
GET /api/feedbacks?pageNumber=1&pageSize=10
```

- `pageNumber` — default `1`, clamped to a minimum of `1`.
- `pageSize` — default `10`, clamped between `1` and `50`.

Response shape:

```json
{
  "items": [ /* FeedbackReadDto[] */ ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 2,
  "totalPages": 1
}
```

HousingService is not included yet — it doesn't currently build (missing enum types), so it has no controllers/routes wired up.

### Authentication (FeedbackService)

Every FeedbackService endpoint requires `Authorization: Bearer <token>` — a JWT issued by AuthService (Node.js), signed with RS256 and verified locally against AuthService's public key (no call to AuthService per request). Payload shape:

```json
{ "sub": "<userId>", "role": "user|admin|super_admin", "email": "...", "iat": ..., "exp": ... }
```

- **`StudentId` is never accepted from the client.** `POST /api/feedbacks` and `POST /api/feedbacks/with-images` derive it from the token's `sub` claim — any `studentId` sent in the request body is ignored. Same reasoning applies to `PUT` — it no longer accepts `studentId` at all.
- **`StudentId` is a `string`, not a number** — AuthService's `sub` is a Mongo ObjectId (e.g. `"6a74f8579ae122df4c9c063d"`), not an integer. `FeedbackReadDto.studentId` and the underlying DB column (`nvarchar(64)`) were changed to match. Client code should treat it as an opaque string id, never parse it as a number.
- **Role `user` gets full CRUD, scoped to their own feedback only.** They can create, list (`GET /api/feedbacks` and `GET /api/feedbacks/mine` both auto-filter to their own records), view, update, and delete — but only records where the token's `sub` matches the record's `StudentId`. Touching someone else's feedback returns `403 Forbidden`.
- **`admin`/`super_admin` have unrestricted access** — `GET /api/feedbacks` returns every record (no owner filter), and they can view/update/delete any feedback regardless of owner.
- `GET /api/feedbacks/mine` always returns only the caller's own feedback (paginated), for any role — a dedicated "my feedback" endpoint distinct from the role-dependent behavior of the plain list endpoint.
- Both `FeedbackImages` delete endpoints (`DELETE /api/feedbackimages/{id}` and `/by-path`) are still `admin`/`super_admin` only — a `user` cleans up their own images by deleting the parent feedback (cascades), not by deleting individual images directly.
- The public key is read from the `JWT_ACCESS_PUBLIC_KEY` environment variable first, falling back to `Jwt:AccessPublicKey` in `appsettings.json` (the key currently committed there is AuthService's real public key — safe to commit, it's the public half of the pair).

### Authentication (AdvertisingService)

Reads are public — `GET /api/ads`, `GET /api/ads/{id}`, `GET /api/ads/active`, `GET /api/ad-types`, `GET /api/target-genders` need no token, since ads are meant to be visible to anyone browsing the app.

Writes require `Authorization: Bearer <token>` and role `admin`/`super_admin` — `POST /api/ads`, `PUT /api/ads/{id}`, `DELETE /api/ads/{id}`. There's no `user`-owns-their-own-ad concept like FeedbackService; ads are administrator-managed content, not user-generated, so there's no ownership check to bypass for admins.

- **`X-User-Id` header is gone.** `CreatedBy` used to come from a client-supplied `X-User-Id` header with zero verification — anyone could impersonate anyone. It's now derived from the token's `sub` claim, same as FeedbackService's `StudentId`.
- **`CreatedBy` is a `string`, not a `Guid`** — same Mongo-ObjectId-vs-Guid mismatch FeedbackService hit; fixed the same way (`nvarchar(64)` column, `ChangeCreatedByToString` migration).
- Creating an ad now also calls `INotificationPublisher.NotifyRoleAsync("user", ...)` — see the NotificationService section below.

### NotificationService

New service, same structure as FeedbackService (Controllers/DTOs/Models/Data/Interfaces/Services/Enums/Migrations, JWT auth via SharedKernel, CORS, always-on Swagger, Dockerfile). Pushes to mobile via Firebase Cloud Messaging (FCM). Its own DB (`NotificationServiceDb`), independent of FeedbackService/AdvertisingService.

**Data model**: `Notification` (the composed message — title/body/optional `data` JSON payload/target/who created it/when sent), `NotificationRecipient` (per-user delivery + read-status row, one per targeted student), `DeviceToken` (FCM token registry, keyed by StudentId, with the role cached at registration time for role-based targeting).

**Roles** (same `user`/`admin`/`super_admin` model as FeedbackService):
- Composing/managing notifications (`GET`/`POST`/`PUT`/`DELETE /api/notifications`, `GET /api/notifications/{id}`) is **admin/super_admin only** — this is a broadcast tool, not something a `user` posts.
- **Any role** can read its own inbox (`GET /api/notifications/mine`, paginated) and mark an item read (`POST /api/notifications/{id}/read`) — id always comes from the token, same pattern as FeedbackService's `/mine`.
- `POST /api/device-tokens` (register/refresh the caller's own FCM token) and `DELETE /api/device-tokens?fcmToken=...` (unregister, e.g. on logout) — any authenticated role, mobile app calls these directly.

**Sending** (`POST /api/notifications`, admin/super_admin, JWT-authenticated):
```json
{
  "title": "...",
  "body": "...",
  "data": "{\"optional\":\"deep-link payload, any JSON you want, opaque to the server\"}",
  "targetType": 0,
  "targetStudentIds": ["<studentId>"],
  "targetRole": null
}
```
`targetType`: `0` = User (exactly one id in `targetStudentIds`), `1` = Users (one or more ids), `2` = Role (`targetRole` required, e.g. `"user"`), `3` = Broadcast (everyone with a registered device token). For `Role`/`Broadcast`, the recipient list is derived from `DeviceTokens` — a student who has never registered a device token won't get an inbox entry either, since NotificationService doesn't own the full student roster (that lives in AuthService). For `User`/`Users`, the caller-supplied ids always get an inbox entry regardless of whether they have a registered token (push just won't fire for them).

**The "public function any service can use"** — this is `SharedKernel.Notifications.INotificationPublisher`, not a REST call you build by hand. Any service that references SharedKernel gets it:

```csharp
// Program.cs
builder.Services.AddSharedNotificationPublisher(builder.Configuration, "YourServiceName");

// wherever you want to notify students
await _notificationPublisher.NotifyRoleAsync("user", "إعلان جديد", ad.Title, data: jsonPayload);
// or NotifyUserAsync(studentId, ...), NotifyUsersAsync(studentIds, ...), BroadcastAsync(...)
```
Every method is safe to `await` with no try/catch — a delivery failure is logged and swallowed internally, never thrown, so notifying students can never break the caller's own operation (this is exactly how AdvertisingService now notifies students when a new ad is created — see `AdvertisementService.CreateAsync`). Needs two settings on the *calling* service: `NOTIFICATION_SERVICE_BASE_URL`/`NotificationService:BaseUrl` and `INTERNAL_SERVICE_KEY`/`InternalApi:ServiceKey` (same env-var-first-then-config pattern as everywhere else in this repo).

Under the hood this hits `POST /api/notifications/broadcast` on NotificationService — a **service-to-service-only** endpoint, no user JWT, protected instead by a shared secret header (`X-Service-Key`, checked by `SharedKernel.Auth.RequireServiceKeyAttribute` against `INTERNAL_SERVICE_KEY`/`InternalApi:ServiceKey`). That same key must be configured identically on NotificationService and on every service that calls it. You generally shouldn't need to call this endpoint directly — use `INotificationPublisher`.

**Firebase**: `Firebase:ServiceAccountJson` (config) or `FIREBASE_SERVICE_ACCOUNT_JSON` (env var) — paste the *entire* service account JSON downloaded from the Firebase console as one string. Resolves lazily on first send (same pattern as Cloudinary), so the service boots fine before real credentials are supplied — sends just silently deliver to 0 recipients until they're set, notifications/recipients still get created and show up in-app.

## Regenerating

These are point-in-time snapshots — regenerate them whenever a service's DTOs or routes change:

```bash
dotnet run --project FeedbackService/FeedbackService.csproj --urls http://localhost:5069 &
curl -s http://localhost:5069/swagger/v1/swagger.json -o contracts/feedback-service.openapi.json

dotnet run --project AdvertisingService/AdvertisingService.csproj --urls http://localhost:5000 &
curl -s http://localhost:5000/swagger/v1/swagger.json -o contracts/advertising-service.openapi.json

dotnet run --project NotificationService/NotificationService.csproj --urls http://localhost:5081 &
curl -s http://localhost:5081/swagger/v1/swagger.json -o contracts/notification-service.openapi.json
```

Or, for always-live docs during active development, point client teams straight at the Gateway's aggregated Swagger UI (`http://localhost:5067/swagger/index.html`) instead of a static file.
