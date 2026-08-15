# API Contracts

OpenAPI 3 specs for each backend service, exported from their live `/swagger/v1/swagger.json` endpoints. The dashboard and mobile app teams can feed these into a codegen tool (e.g. `openapi-generator`, `NSwag`, `orval`) to produce typed HTTP clients instead of hand-writing request/response models.

All requests should go through the Gateway (`http://localhost:5067` locally), using the same paths as in each spec below — Ocelot forwards paths unchanged.

| File | Service | Base path (via Gateway) |
|---|---|---|
| `feedback-service.openapi.json` | FeedbackService | `/api/feedbacks`, `/api/feedbackimages` |
| `advertising-service.openapi.json` | AdvertisingService | `/api/ads`, `/api/ad-types`, `/api/target-genders`, `/api/advertisementimages` |

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
{ "sub": "<userId>", "role": "student|admin|super_admin", "email": "...", "iat": ..., "exp": ... }
```

- **`StudentId` is never accepted from the client.** `POST /api/feedbacks` and `POST /api/feedbacks/with-images` derive it from the token's `sub` claim — any `studentId` sent in the request body is ignored. Same reasoning applies to `PUT` — it no longer accepts `studentId` at all.
- **`StudentId` is a `string`, not a number** — AuthService's `sub` is a Mongo ObjectId (e.g. `"6a74f8579ae122df4c9c063d"`), not an integer. `FeedbackReadDto.studentId` and the underlying DB column (`nvarchar(64)`) were changed to match. Client code should treat it as an opaque string id, never parse it as a number.
- `GET /api/feedbacks` (list all), `PUT /api/feedbacks/{id}`, `DELETE /api/feedbacks/{id}`, and both `FeedbackImages` delete endpoints require role `admin` or `super_admin`.
- `GET /api/feedbacks/{id}` — a `student` may only fetch their own feedback (`sub` must match the record's `StudentId`); `admin`/`super_admin` can fetch any.
- The public key is read from the `JWT_ACCESS_PUBLIC_KEY` environment variable first, falling back to `Jwt:AccessPublicKey` in `appsettings.json` (the key currently committed there is AuthService's real public key — safe to commit, it's the public half of the pair).

## Regenerating

These are point-in-time snapshots — regenerate them whenever a service's DTOs or routes change:

```bash
dotnet run --project FeedbackService/FeedbackService.csproj --urls http://localhost:5069 &
curl -s http://localhost:5069/swagger/v1/swagger.json -o contracts/feedback-service.openapi.json

dotnet run --project AdvertisingService/AdvertisingService.csproj --urls http://localhost:5000 &
curl -s http://localhost:5000/swagger/v1/swagger.json -o contracts/advertising-service.openapi.json
```

Or, for always-live docs during active development, point client teams straight at the Gateway's aggregated Swagger UI (`http://localhost:5067/swagger/index.html`) instead of a static file.
