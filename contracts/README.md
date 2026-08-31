# API Contracts

OpenAPI 3 specs for each backend service, exported from their live `/swagger/v1/swagger.json` endpoints. The dashboard and mobile app teams can feed these into a codegen tool (e.g. `openapi-generator`, `NSwag`, `orval`) to produce typed HTTP clients instead of hand-writing request/response models.

All requests should go through the Gateway (`http://localhost:5067` locally), using the same paths as in each spec below — Ocelot forwards paths unchanged.

| File | Service | Base path (via Gateway) |
|---|---|---|
| `feedback-service.openapi.json` | FeedbackService | `/api/feedbacks`, `/api/feedbackimages` |
| `advertising-service.openapi.json` | AdvertisingService | `/api/ads`, `/api/ad-types`, `/api/target-genders` |
| `notification-service.openapi.json` | NotificationService | `/api/notifications` |
| `housing-service.openapi.json` | HousingService | `/api/buildings`, `/api/housing-cycles`, `/api/governorates`, `/api/housing-requests`, `/api/housing-groups`, `/api/allocations` |
| *(external, no local snapshot)* | AuthService (Node.js) | `/api/auth`, `/api/admin` |

### AuthService is routed through the Gateway too

`/api/auth/*` and `/api/admin/*` proxy straight to the real deployed AuthService (`https://university-auth-lemon.vercel.app`) in every Ocelot config (`ocelot.json`, `ocelot.docker.json`, `ocelot.Production.json` all point at the same URL — unlike our own services, AuthService has no separate local/Docker instance we control). Its Swagger doc (`https://university-auth-lemon.vercel.app/api-docs.json`) is aggregated into the Gateway's Swagger UI the same way the other services are.

**`/api/internal/*` (user lookup, FCM token listing) is deliberately NOT routed through the Gateway.** Those endpoints are server-to-server only — protected by `X-Internal-Api-Key`, not a user JWT — and AuthService's own OpenAPI doc says as much ("Never call these from a browser or with a user's JWT"). Exposing the route publicly wouldn't be a security hole by itself (the key check still applies), but it's unnecessary attack surface for zero benefit: our services (`SharedKernel.Users.IUserLookupService`, NotificationService's FCM lookup) already call AuthService directly using its base URL, never through the Gateway, for exactly this reason.

### Admin dashboard aggregate (`GET /api/dashboard`)

The Gateway exposes one aggregate endpoint that fans out to the two per-service dashboard
endpoints and **merges their JSON into a single flat object** (top-level keys don't overlap):

| Upstream (Gateway) | Downstream | Auth |
|---|---|---|
| `GET /api/dashboard` | HousingService `GET /api/housing-requests/dashboard` + FeedbackService `GET /api/feedbacks/dashboard` | `admin` / `super_admin` Bearer token, forwarded to both |
| `GET /api/dashboard/housing` | just the housing half | same |
| `GET /api/dashboard/feedback` | just the feedback half | same |

Housing keys: `pendingRequests`, `occupancyRate`, `occupiedBeds`, `totalBeds`,
`totalHousedStudents`, `rooms`, `recentRequests`, `weeklyOccupancy`.
Feedback keys: `openComplaints`, `unreadCount`, `totalComplaints`, `totalSuggestions`,
`recentFeedback`.
If either downstream returns a non-200 (e.g. token missing/expired), that response is passed
straight through instead of a half-built body. Aggregation is GET-only (Ocelot
`Aggregates` + a custom `DashboardAggregator`).

**Frontend hand-off:** `frontend-dashboard-and-payment.md` in this folder has the full
response shapes, enum mappings, and the housing-fee payment / settings endpoints, written for
the dashboard and student-payment UI work. `frontend-housing-requests-filters.md` documents
the query filters on `GET /api/housing-requests` (cycle, governorate, status, studentIds,
academicLevel, gender, isPaid, specialNeeds, grouped, submitted-date range).
`frontend-previous-residence-picker.md` covers the student-safe `GET /api/buildings/lookup`
(now with `floorsCount`) and `GET /api/buildings/{buildingId}/rooms/lookup` used to pick a
previous building/floor/room on a housing request.

### Images are Cloudinary URLs, not local paths

`FeedbackImageDto.imagePath` and `AdvertisementDto.imageUrl` are now full, absolute Cloudinary URLs (e.g. `https://res.cloudinary.com/<cloud>/image/upload/v.../feedback/xyz.png`), publicly fetchable directly — **do not** prefix them with the Gateway or service origin, and don't route them through `/api/...`. Both services used to store images on local disk and serve them via `/uploads/...` or a local proxy endpoint; that's gone (the old `/api/advertisementimages/advertisements/{fileName}` endpoint was removed). This also means images now work correctly across independently-hosted services — no gateway routing needed for them at all.

Rows created before this migration may still hold old local-style paths; those won't resolve unless the originating service is still running with its old `wwwroot/uploads` content.

### Pagination

Paginated endpoints take `pageNumber`/`pageSize` via query string, not a request body (GET requests with bodies aren't reliably supported by HTTP tooling/clients):

```
GET /api/feedbacks?pageNumber=1&pageSize=10
GET /api/housing-requests?pageNumber=1&pageSize=10
GET /api/housing-groups?pageNumber=1&pageSize=10
GET /api/allocations?pageNumber=1&pageSize=10
```

- `pageNumber` — default `1`, clamped to a minimum of `1`.
- `pageSize` — default `10`, clamped between `1` and `50`.

Response shape (identical across every service — `FeedbackService`, `NotificationService`, and `HousingService` all define the same `PagedResult<T>`/`PaginationParams` pair independently, so client codegen sees the same envelope regardless of which service the endpoint belongs to):

```json
{
  "items": [ /* T[] */ ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalCount": 2,
  "totalPages": 1
}
```

In HousingService this applies to `GET /api/housing-requests`, `GET /api/housing-groups`, and `GET /api/allocations` — the admin-facing list endpoints whose result sets grow over time. The smaller reference/lookup endpoints (`/api/governorates`, `/api/housing-cycles`, `/api/buildings/lookup`, `/api/allocations/candidate-rooms`) return a plain array, same as equivalent lookup endpoints in other services (e.g. `/api/ad-types`, `/api/target-genders`).

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

**Student/admin names on `GET /api/feedbacks`**: `studentName` and `repliedByAdminName` are populated from AuthService's `POST /api/internal/users/lookup` (one batched call per page, all unique `studentId`/`repliedByAdminId` values on that page — never one call per item). `studentName` stays `null` for anonymous feedback even though `studentId` is known internally — anonymity is enforced at display time, not by hiding the id from the DB. If AuthService is unreachable or rejects the call, the whole request still succeeds — names just come back `null` (this is `SharedKernel.Users.IUserLookupService`, same never-throws design as `INotificationPublisher`). Needs `AuthService:BaseUrl`/`AUTH_SERVICE_BASE_URL` and `AuthService:InternalApiKey`/`AUTH_SERVICE_INTERNAL_API_KEY` configured — the real key is set locally now and verified end-to-end (a real, non-anonymous feedback row correctly returned its actual `studentName` from AuthService). `GET /api/feedbacks/{id}` and `/mine` don't have this enrichment yet — only the list endpoint, per what was actually asked for.

### Authentication (AdvertisingService)

Reads are public — `GET /api/ads`, `GET /api/ads/{id}`, `GET /api/ads/active`, `GET /api/ad-types`, `GET /api/target-genders` need no token, since ads are meant to be visible to anyone browsing the app.

Writes require `Authorization: Bearer <token>` and role `admin`/`super_admin` — `POST /api/ads`, `PUT /api/ads/{id}`, `DELETE /api/ads/{id}`. There's no `user`-owns-their-own-ad concept like FeedbackService; ads are administrator-managed content, not user-generated, so there's no ownership check to bypass for admins.

- **`X-User-Id` header is gone.** `CreatedBy` used to come from a client-supplied `X-User-Id` header with zero verification — anyone could impersonate anyone. It's now derived from the token's `sub` claim, same as FeedbackService's `StudentId`.
- **`CreatedBy` is a `string`, not a `Guid`** — same Mongo-ObjectId-vs-Guid mismatch FeedbackService hit; fixed the same way (`nvarchar(64)` column, `ChangeCreatedByToString` migration).
- Creating an ad now also calls `INotificationPublisher.NotifyRoleAsync("user", ...)` — see the NotificationService section below.

### NotificationService

New service, same structure as FeedbackService (Controllers/DTOs/Models/Data/Interfaces/Services/Enums/Migrations, JWT auth via SharedKernel, CORS, always-on Swagger, Dockerfile). Pushes to mobile via Firebase Cloud Messaging (FCM). Its own DB (`NotificationServiceDb`), independent of FeedbackService/AdvertisingService.

**Data model**: `Notification` (the composed message — title/body/optional `data` JSON payload/target/who created it/when sent), `NotificationRecipient` (per-user delivery + read-status row, one per targeted student). There is no local device-token table — AuthService is the single source of truth for FCM tokens (see below).

**Roles** (same `user`/`admin`/`super_admin` model as FeedbackService):
- Composing/managing notifications (`GET`/`POST`/`PUT`/`DELETE /api/notifications`, `GET /api/notifications/{id}`) is **admin/super_admin only** — this is a broadcast tool, not something a `user` posts.
- **Any role** can read its own inbox (`GET /api/notifications/mine`, paginated) and mark an item read (`POST /api/notifications/{id}/read`) — id always comes from the token, same pattern as FeedbackService's `/mine`.
- There's no device-token registration endpoint on NotificationService anymore. The mobile app sends its FCM token to AuthService (at login/registration), not here.

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
`targetType`: `0` = User (exactly one id in `targetStudentIds`), `1` = Users (one or more ids), `2` = Role (`targetRole` required, e.g. `"user"`), `3` = Broadcast (everyone with a registered device token). For `Role`/`Broadcast`, both the recipient list and the FCM tokens come from AuthService's `GET /api/internal/users/fcm-tokens` (active users only, already filtered to those with a token) — a student who never registered a device token won't get an inbox entry for these. For `User`/`Users`, the caller-supplied ids always get an inbox entry regardless of whether they have a registered token (push just won't fire for them); tokens for the push itself come from AuthService's `POST /api/internal/users/lookup`.

**FCM tokens live in AuthService, not here.** NotificationService calls AuthService's internal endpoints (`X-Internal-Api-Key` header) on every send instead of keeping its own device-token table — one less place a token can go stale. Needs `AuthService:BaseUrl`/`AUTH_SERVICE_BASE_URL` and `AuthService:InternalApiKey`/`AUTH_SERVICE_INTERNAL_API_KEY` configured (same env-var-first-then-config pattern as everywhere else); the app fails fast at startup if either is missing. Like `IUserLookupService`, a failed AuthService call never throws — it just means fewer/no recipients get a push for that send (they still get an in-app inbox entry where applicable).

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

dotnet run --project HousingService/HousingService/HousingService.csproj --urls http://localhost:5054 &
curl -s http://localhost:5054/swagger/v1/swagger.json -o contracts/housing-service.openapi.json
```

Or, for always-live docs during active development, point client teams straight at the Gateway's aggregated Swagger UI (`http://localhost:5067/swagger/index.html`) instead of a static file.
