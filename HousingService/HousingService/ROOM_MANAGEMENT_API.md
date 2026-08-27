# Room Management APIs — Frontend Integration Guide

Covers the three admin actions that change who occupies a room after allocation:
**vacate** (remove everyone), **remove one group member** (keep the rest), and **transfer** (move to a different room).

All three live on `AllocationsController` (`/api/allocations`) and are **admin/super_admin only**.

## Base URL & Auth

Call these through the Gateway, same path, unchanged:

```
http://localhost:5067/api/allocations/...     (local dev)
```

Every request needs:

```
Authorization: Bearer <admin JWT>
Content-Type: application/json
```

The token's `role` claim must be `admin` or `super_admin` — students get `403 Forbidden` on all three.

## Finding the `{id}` (allocation id) first

All three endpoints operate on an **allocation id**, not a student id or room id directly. Get it from whichever of these fits the screen you're building:

| Endpoint | Use case |
|---|---|
| `GET /api/allocations?buildingId=&roomId=&pageNumber=&pageSize=` | Browse allocations by building/room (paginated) |
| `GET /api/allocations/{id}` | Look up one allocation by its own id |
| `GET /api/allocations/students/{studentId}/history` | Every allocation (active + past) a specific student has ever had |
| `GET /api/allocations/mine` | (student-facing, not admin) the caller's own current allocation |

Every one of these returns (or lists) an `AllocationDto`:

```json
{
  "id": 42,
  "housingRequestId": 17,
  "housingGroupId": null,
  "roomId": 205,
  "roomNumber": "304",
  "buildingId": 3,
  "buildingName": "3",
  "occupantStudentIds": ["66f1a2b3c4d5e6f7a8b9c0d1"],
  "allocatedAt": "2026-08-20T10:00:00Z",
  "vacatedAt": null
}
```

- `housingRequestId` is set for an **individual** allocation, `housingGroupId` for a **group** allocation — exactly one of the two is non-null.
- `occupantStudentIds` is always the live, current list of who's actually in that room (one id for an individual allocation, all current group members for a group one).
- `vacatedAt: null` means the allocation is still active. Once vacated, it's forever `non-null` on that row (history is preserved, never deleted).

---

## 1. Vacate — remove everyone from a room

```
POST /api/allocations/{id}/vacate
```

**Body** (optional):
```json
{ "message": "غرفة تحتاج صيانة عاجلة، الرجاء الإخلاء." }
```
Omit `message` (or send `{}`) to use the default Arabic notification text.

**What it does:** Ends the **whole** allocation — if it's an individual student, they're out; if it's a group, **every current member** of that group is evicted from the room at once. Stamps `vacatedAt`, recomputes the room's `Status` (`Available`/`Occupied`/`Full`), and sends a notification to every affected student (in-app, via NotificationService — no extra call needed from the frontend, this happens server-side).

**Use this when:** the room itself needs to be emptied out entirely (maintenance, the whole group is being dissolved, etc.) — not for pulling just one person out of a shared group room.

**Responses:**
| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AllocationDto` | Vacated. `vacatedAt` is now set on the returned object. |
| `404 Not Found` | — | No allocation with that id. |
| `400 Bad Request` | `"This allocation has already been vacated."` | Calling it twice on the same allocation. |

---

## 2. Remove one group member — keep the rest housed

```
POST /api/allocations/{id}/members/{studentId}/remove
```

No request body.

**What it does:** Drops **one specific student** out of a shared group room while the rest of the group stays. Recomputes the room's `Status` (one fewer occupant), and internally re-runs the same leave/remove logic used elsewhere (leadership transfer if the removed student was the leader, notification to the remaining members that someone left). If the removed student happens to be the group's **last** remaining member, this automatically becomes a full vacate too (the allocation's `vacatedAt` gets stamped) — you don't need to call `/vacate` afterward in that case.

**Use this when:** a single member of a housed group needs to leave/be kicked (disciplinary, they dropped out, etc.) but the rest of the group keeps their room.

**Important:** `{id}` here must be a **group** allocation (`housingGroupId` non-null). Calling this on an individual allocation returns an error telling you to use `/vacate` instead — there's no "partial" removal concept for a single-occupant allocation.

**Responses:**
| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AllocationDto` (refreshed) | Member removed. Check `occupantStudentIds` — it no longer includes `{studentId}`. |
| `404 Not Found` | — | No allocation with that id. |
| `400 Bad Request` | `"This allocation is for an individual student, not a group. Use the vacate endpoint to remove them instead."` | Wrong endpoint for an individual allocation. |
| `400 Bad Request` | `"This allocation has already been vacated."` | Allocation is no longer active. |
| `400 Bad Request` | `"This student is not a member of the allocated group."` | `{studentId}` isn't actually in that group. |

---

## 3. Transfer — move to a different room

```
POST /api/allocations/{id}/transfer
```

**Body** (required):
```json
{ "newRoomId": 217 }
```

**What it does:** Moves the allocation's occupant(s) — the individual student, or the *entire* group — from their current room to `newRoomId`, in one step. Re-validates everything a fresh allocation would (target building's gender matches, target room isn't `Maintenance`/`Closed`, target room has enough free seats for 1 person or the whole group). Updates both the **old** room's status (frees up its seat(s)) and the **new** room's status, then notifies every affected student with the new room/building name. The same `Allocation` row is reused (its `RoomId` just changes) — it does **not** create a new allocation record or vacate-then-recreate.

**Use this when:** a student/group needs to move rooms but stay housed — e.g. correcting a wrong initial assignment, consolidating groups, accessibility needs.

**Responses:**
| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AllocationDto` | Moved. `roomId`/`roomNumber`/`buildingId`/`buildingName` now reflect the new room. |
| `404 Not Found` | — | No allocation with that id. |
| `400 Bad Request` | `"This allocation has already been vacated and can no longer be transferred."` | Can't move someone who's no longer housed. |
| `400 Bad Request` | `"The student is already assigned to this room."` | `newRoomId` equals the current room — no-op, rejected. |
| `400 Bad Request` | `"Room was not found."` | `newRoomId` doesn't exist. |
| `400 Bad Request` | `"This room's building does not match the required gender."` | Target building's gender ≠ the occupant's (or group leader's) gender. |
| `400 Bad Request` | `"This room is not available for allocation."` | Target room is `Maintenance` or `Closed`. |
| `400 Bad Request` | `"This room does not have enough remaining capacity."` | Not enough free seats in the target room. |

---

## Quick decision guide for the UI

| You want to... | Call |
|---|---|
| Empty out a room entirely (individual or whole group) | `POST /allocations/{id}/vacate` |
| Kick one person out of a group's room, everyone else stays | `POST /allocations/{id}/members/{studentId}/remove` |
| Move a student/group to a different room, still housed | `POST /allocations/{id}/transfer` |

A natural admin UI pattern: on a room's detail screen (list of `occupantStudentIds`), show a "Remove" button per student — call `/members/{studentId}/remove` if it's a group room and more than one student is listed, or `/vacate` if it's a single-occupant (individual) room. A "Move room" button on the same screen opens a room picker (you can source candidates from `GET /api/allocations/candidate-rooms?housingGroupId=` / `?housingRequestId=`) and calls `/transfer` with the chosen `newRoomId`.

## Notifications

All three actions push an in-app notification to the affected student(s) automatically (server-side, via NotificationService) — no separate call needed from the frontend to inform the student. If you have a notifications inbox screen, it reads from NotificationService's own `GET /api/notifications/mine`, independent of these calls.
