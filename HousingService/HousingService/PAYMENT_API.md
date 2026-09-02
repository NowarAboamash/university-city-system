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
  "housingFeeAmount": 0,        // >= 0, at most 2 decimals; must be > 0 before payments work
  "updatedAt": null            // response only
}
```

`housingFeeAmount` is in whole currency units (25 = 25 dinar). auth-service's wallet stores it
as a plain JSON number, so it's capped at 2 decimal places.

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

Paying is allowed right up until the deadline day passes. Once the daily job evicts an
overdue-unpaid request (see below), its decision becomes `Rejected` and `/pay` then returns
`400` (not accepted) — there is no late payment and no reinstatement; the student reapplies
in a future cycle.

Concurrent `/pay` calls for the same request are safe: auth-service is idempotent on
`(userId, reference)`, so a duplicate charge with `housing-request-{id}` returns 200 with the
original balance and moves no money.

Config: reuses `AuthService:BaseUrl` + `AuthService:InternalApiKey`
(`AUTH_SERVICE_BASE_URL` / `AUTH_SERVICE_INTERNAL_API_KEY`) — the same internal key already
used for user lookup. No new settings.

## Daily job — `PaymentReminderJob`

A single `BackgroundService` runs every 24h and does two disjoint passes:

**1. Eviction — `IUnpaidEvictionService.RunAsync()` (runs first).**
Every request that is **Accepted, still unpaid**, and whose `PaymentDueDate` day has *fully
passed* (`PaymentDueDate < today` — the 15th day itself still belongs to the student, and
there is **no grace period**) is evicted:

- its `AdmissionDecision` is set to `Rejected` with `RejectionReason = NonPayment`;
- that cascades through the normal decision-reversal path — an individual's active
  `Allocation` is vacated (room freed), a grouped member is dropped from the group (roommates
  stay housed, the room re-synced), the last member of a group also vacates its allocation;
- the student gets one notification worded for non-payment.

Eviction is final for the cycle: no reinstatement, no late payment. To get housing again the
student applies afresh in a future cycle.

**2. Reminder — `IPaymentReminderService.RunAsync()`.**
Notifies every request that is **Accepted, unpaid, not yet reminded**, and whose
`PaymentDueDate` is within `ReminderDaysBefore` days (overdue-but-not-yet-evicted included),
then sets `ReminderSent = true`.

Notification `data`:
`{ "type": "housing_payment_reminder", "relatedId": <requestId> }` (reminder),
`{ "type": "housing_payment_completed", "relatedId": <requestId> }` (paid).
Eviction reuses the decision-changed notification (no dedicated `data` payload).

> Note: if housing-service is deployed on a free host that sleeps while idle, the in-process
> job may miss its tick. Both passes are isolated behind their own interfaces so a thin
> authenticated "run now" endpoint + an external cron can be added later without reworking the
> logic.

## `RejectionReason`

`AdmissionDecision` carries a nullable `RejectionReason` (exposed on `AdmissionDecisionDto` /
`HousingRequestDto.decision`), meaningful only when `status == Rejected`:

| Value | Meaning |
|---|---|
| `AdminReview` (0) | Rejected by an admin during application review (the default for any bare rejection). |
| `NonPayment` (1) | Auto-evicted by the daily job for not paying within the deadline. |

`POST /api/housing-requests/{id}/decision` accepts an optional `rejectionReason`; omitting it
on a `Rejected` decision defaults to `AdminReview`. Moving a decision off `Rejected` clears it.
