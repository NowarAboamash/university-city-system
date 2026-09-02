# Automatic room assignment — Frontend Integration Guide

One admin endpoint that bulk-places every accepted-but-not-yet-housed request and group of the
**current open cycle** into the available rooms. Lives on `AllocationsController`, **admin /
super_admin only**.

```
POST /api/allocations/auto-assign
Authorization: Bearer <admin JWT>
Content-Type: application/json
```

## Request

```json
{ "dryRun": true }
```

| Field | Meaning |
|---|---|
| `dryRun` | `true` → compute and return the proposed plan, write **nothing**. `false` → commit it. |

Recommended flow: call once with `dryRun: true`, show the plan for review, then call again
with `dryRun: false` to apply it.

## What it places

- Every `HousingRequest` in the open cycle whose `AdmissionDecision.status == Accepted` that
  doesn't already hold an active allocation — individuals **and** groups.
- **Payment is not a precondition.** Accepted students are housed regardless of whether the
  fee is paid; non-payment is handled separately by the daily eviction job (see `PAYMENT_API.md`).
- A group is placed only if **every** member is `Accepted`. A group with any non-accepted
  member is reported under `skipped`, not placed.
- Already-housed requests/groups are silently ignored (neither placed nor skipped).

## The algorithm

Bin-packing tuned to house as many students as possible:

1. **Groups first, largest first** (then oldest-accepted). A group of N needs a single room
   with ≥ N free beds — it's never split. Each group goes into the **tightest-fitting** room
   (smallest sufficient free space), keeping roomier rooms free for bigger groups.
2. **Individuals next**, oldest-accepted first, each into the **most-full room that still has a
   bed** — consolidating people so whole empty rooms stay clean for later.
3. Gender is always respected (a room's building gender must match the student / group leader).
   Rooms that are `Maintenance` / `Closed` / `Full` are excluded.
4. Previous-residence ("give me my old room") is **not** considered in this version.

On `dryRun: false` each planned placement is re-validated and committed through the same path
as a manual `POST /api/allocations` (so a race with another admin can't double-book a room);
any placement that fails the re-check is moved to `skipped` with the reason, the rest still commit.

## Response — `AutoAssignResultDto`

```json
{
  "dryRun": true,
  "placedTargets": 128,
  "housedStudents": 372,
  "skippedTargets": 3,
  "assignments": [
    {
      "housingRequestId": null,
      "housingGroupId": 44,
      "size": 4,
      "roomId": 1021,
      "roomNumber": "312",
      "buildingId": 5,
      "buildingName": "5",
      "studentIds": ["...", "...", "...", "..."]
    },
    {
      "housingRequestId": 981,
      "housingGroupId": null,
      "size": 1,
      "roomId": 1021,
      "roomNumber": "312",
      "buildingId": 5,
      "buildingName": "5",
      "studentIds": ["..."]
    }
  ],
  "skipped": [
    { "targetType": "group", "targetId": 51, "size": 3, "reason": "Not all group members have been accepted yet." },
    { "targetType": "individual", "targetId": 990, "size": 1, "reason": "No available room has a free bed." },
    { "targetType": "group", "targetId": 52, "size": 4, "reason": "No available room has 4 free beds together for the group." }
  ]
}
```

- `placedTargets` = number of individuals + groups placed; `housedStudents` = sum of `size`
  (people). On a dry run these describe the plan; on a commit they describe what actually persisted.
- Each `assignment` has exactly one of `housingRequestId` / `housingGroupId` set.
- On a committed run, a placement rejected during the re-check appears in `skipped` with
  `reason` prefixed `"Rejected at commit: ..."` and is **not** in `assignments`.

## Responses

| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AutoAssignResultDto` | Plan (dry run) or commit result. |
| `400 Bad Request` | `"No housing cycle is currently open."` | Open a cycle first. |

Committed assignments send each affected student the normal "تم تخصيص سكنك" notification
(server-side, via NotificationService) — same as a manual allocation.
