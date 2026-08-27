# Room Management APIs — Frontend Integration Guide

Covers the admin actions that change who occupies a room after allocation:
**vacate** (remove everyone), **remove one group member** (keep the rest), **transfer** (move to a different room) — plus **student-centric** versions of remove/transfer that take a student id directly instead of an allocation id.

All of these live on `AllocationsController` (`/api/allocations`) and are **admin/super_admin only**.

**Two ways to call remove/transfer:**
- **By allocation id** (`/{id}/vacate`, `/{id}/members/{studentId}/remove`, `/{id}/transfer`) — you already know which allocation/room you're acting on (e.g. you're on a room's detail screen).
- **By student id** (`/students/{studentId}/vacate`, `/students/{studentId}/transfer`) — you only have the student, not their allocation id or which room they're in (e.g. you're on a student's profile screen). The server figures out whether they're housed individually or as part of a group and does the right thing either way. **This is usually the simpler choice for an admin UI built around students, not rooms.**

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

## 4. Remove a specific student — student-centric, no allocation lookup needed

```
POST /api/allocations/students/{studentId}/vacate
```

**Body** (optional, same as `/vacate`):
```json
{ "message": "..." }
```

**What it does:** Removes **this one student** from wherever they currently live — you don't need to know their allocation id, their room, or whether they're housed individually or as part of a group. The server resolves their current active allocation and then does exactly what `/vacate` or `/members/{studentId}/remove` would:
- Individual student → their allocation ends (same as calling `/vacate` on it).
- Grouped student with roommates remaining → just them leaves, the rest of the group keeps the room (same as calling `/members/{studentId}/remove`).
- Grouped student who's the group's last member → same end result as the individual case (allocation fully ends).

In every case **only this student is affected** — this never evicts a roommate who wasn't named.

**Responses:**
| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AllocationDto` | Removed. If they were grouped with others, this is their (now-updated) group's allocation; if they were the only/individual occupant, `vacatedAt` is now set. |
| `404 Not Found` | `"This student is not currently housed."` | No active allocation for this student at all. |

---

## 5. Move a specific student to a different room — student-centric

```
POST /api/allocations/students/{studentId}/transfer
```

**Body** (required, same as `/transfer`):
```json
{ "newRoomId": 217 }
```

**What it does:** Moves **this one student** to `newRoomId` — again, no allocation id needed up front. Behavior depends on their current situation:
- Individual student (or the only member of their group) → their whole allocation moves, same as calling `/transfer` directly (same allocation id, just a new `roomId`).
- **Grouped student with roommates remaining → they're split off into a brand-new individual allocation in the target room, while the rest of the group stays behind in the old room.** This is the one behavior that has no equivalent on the allocation-id-based `/transfer` (which always moves the *entire* allocation/group together, never one member alone). The response's `id` will be a **new** allocation id, different from the group's original one, and `occupantStudentIds` will contain only this student.

Both the old room (one fewer occupant, group's allocation unaffected otherwise) and the new room (one more occupant) get their `Status` recomputed. The moved student gets the same "تم نقل سكنك" notification as a normal transfer.

**Responses:**
| Status | Body | Meaning |
|---|---|---|
| `200 OK` | `AllocationDto` | Moved. For the split case, this is the **new** allocation (check `id` if you need to distinguish it from the group's original one). |
| `404 Not Found` | `"This student is not currently housed."` | No active allocation for this student at all. |
| `400 Bad Request` | Same validation messages as `/transfer` (already-vacated, same room, gender mismatch, room unavailable, insufficient capacity) | The target room doesn't work for this student. |

---

## Quick decision guide for the UI

| You want to... | Call |
|---|---|
| Empty out a room entirely (individual or whole group), and you're working from the room | `POST /allocations/{id}/vacate` |
| Remove one specific student, and you already have their allocation id and know it's a group | `POST /allocations/{id}/members/{studentId}/remove` |
| **Remove one specific student, working from the student (don't know/care about the allocation id)** | **`POST /allocations/students/{studentId}/vacate`** |
| Move a student/group to a different room, and you're working from the room/allocation | `POST /allocations/{id}/transfer` |
| **Move one specific student to a different room, working from the student — even if they're grouped, only they move** | **`POST /allocations/students/{studentId}/transfer`** |

A natural admin UI pattern: on a **student's** profile screen, a "Remove from housing" button calls `/students/{studentId}/vacate`, and a "Move to another room" button (opening a room picker, sourced from `GET /api/allocations/candidate-rooms?housingRequestId=` for an individual or `?housingGroupId=` for a group) calls `/students/{studentId}/transfer` — you never need to look up an allocation id first. On a **room's** detail screen instead (list of `occupantStudentIds`), the `{id}`-based endpoints are more natural since you already have the allocation loaded.

## Notifications

Every one of these actions pushes an in-app notification to the affected student(s) automatically (server-side, via NotificationService) — no separate call needed from the frontend to inform the student. If you have a notifications inbox screen, it reads from NotificationService's own `GET /api/notifications/mine`, independent of these calls.
