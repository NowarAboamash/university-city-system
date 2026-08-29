# Housing payment: fee, deadline & automatic reminder

All of this lives inside **housing-service**. The only outbound call is a wallet charge on
**auth-service** (`POST /api/internal/wallet/charge`) at the moment a student actually pays.

## Model

`HousingRequest` gained:

| Field | Meaning |
|---|---|
| `PaymentDueDate` (nullable) | Set **once**, the first time the request is `Accepted`, to `acceptedAt + PaymentDeadlineDays`. Never moved afterwards (a later settings change or a decision reversed-then-re-accepted keeps the original date). Stays `null` for requests that were never accepted. |
| `IsPaid` | `true` only after a real HTTP 200 from the wallet charge. |
| `PaidAt` (nullable) | Timestamp of that successful charge. |
| `ReminderSent` | Guards the automatic reminder against firing more than once. |

`HousingRequestDto` exposes `paymentDueDate`, `isPaid`, `paidAt`.

## Settings — `GET`/`PUT /api/housing-requests/settings` (admin)

Single global row, editable from the dashboard.

```jsonc
{
  "paymentDeadlineDays": 15,   // > 0
  "reminderDaysBefore": 3,     // > 0 and < paymentDeadlineDays
  "housingFeeAmount": 0,        // >= 0 ; must be > 0 before payments work
  "updatedAt": null            // response only
}
```

`PUT` validates the constraints above and returns `400` with a message on violation.
`housingFeeAmount` is a single flat fee for every request (all rooms are one tier).

## Pay — `POST /api/housing-requests/{id}/pay` (student, own request)

Charges `housingFeeAmount` from the student's auth-service wallet
(`reference = "housing-request-{id}"`) and marks the request paid on success.

| Outcome | HTTP | Notes |
|---|---|---|
| Success | `200` | `{ "message", "balance" }` — `balance` is the wallet balance after the charge. |
| Request not found | `404` | |
| Not the owner | `403` | |
| Not accepted yet | `400` | Only an `Accepted` request can be paid. |
| Already paid | `409` | |
| Fee not configured (`housingFeeAmount <= 0`) | `409` | Admin must set it first. |
| Insufficient wallet balance | `402` | auth-service returned 402; request stays unpaid. |
| Wallet gateway error | `502` | auth-service unreachable / 5xx / misconfig; request stays unpaid. |

Paying is still allowed after `PaymentDueDate` has passed — the deadline only drives the
reminder; freeing an unpaid spot is a separate manual admin action.

Config: reuses `AuthService:BaseUrl` + `AuthService:InternalApiKey`
(`AUTH_SERVICE_BASE_URL` / `AUTH_SERVICE_INTERNAL_API_KEY`) — the same internal key already
used for user lookup. No new settings.

## Automatic reminder

`PaymentReminderJob` (a `BackgroundService`) runs `IPaymentReminderService.RunAsync()` every
24h. It notifies every request that is **Accepted, unpaid, not yet reminded**, and whose
`PaymentDueDate` is within `ReminderDaysBefore` days (overdue ones included, so a missed run
still sends late rather than skipping), then sets `ReminderSent = true`.

Notification `data`: `{ "type": "housing_payment_reminder", "relatedId": <requestId> }`.
On a successful payment: `{ "type": "housing_payment_completed", "relatedId": <requestId> }`.

> Note: if housing-service is deployed on a free host that sleeps while idle, the in-process
> job may miss its tick. `RunAsync` is deliberately isolated in `IPaymentReminderService` so a
> thin authenticated "run now" endpoint + an external cron can be added later without
> reworking the logic.
