using HousingService.Data.Repositories;
using HousingService.Domain.Entities;
using HousingService.Domain.Enums;
using HousingService.DTOs;
using HousingService.Interfaces;
using SharedKernel.Notifications;

namespace HousingService.Services;

public class AllocationService : IAllocationService
{
    private readonly IAllocationRepository _allocationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IHousingRequestRepository _requestRepository;
    private readonly IHousingGroupRepository _groupRepository;
    private readonly IHousingGroupService _groupService;
    private readonly IHousingCycleRepository _cycleRepository;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public AllocationService(
        IAllocationRepository allocationRepository,
        IRoomRepository roomRepository,
        IHousingRequestRepository requestRepository,
        IHousingGroupRepository groupRepository,
        IHousingGroupService groupService,
        IHousingCycleRepository cycleRepository,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _allocationRepository = allocationRepository;
        _roomRepository = roomRepository;
        _requestRepository = requestRepository;
        _groupRepository = groupRepository;
        _groupService = groupService;
        _cycleRepository = cycleRepository;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<CandidateRoomDto>> GetCandidateRoomsAsync(int? housingRequestId, int? housingGroupId)
    {
        // allowExistingAllocation: this list also feeds "transfer", where the target is already
        // housed. The transfer commit itself still revalidates every rule.
        var (studentGender, neededCapacity, _, _) = await ResolveTargetAsync(housingRequestId, housingGroupId, allowExistingAllocation: true);

        // If the target is already housed, drop their current room from the candidate list —
        // transferring to the same room is a no-op the commit would reject anyway.
        var currentAllocation = housingRequestId is not null
            ? await _allocationRepository.GetByHousingRequestIdAsync(housingRequestId.Value)
            : await _allocationRepository.GetByGroupIdAsync(housingGroupId!.Value);
        var currentRoomId = currentAllocation?.RoomId;

        var rooms = await _roomRepository.GetAllWithOccupantsAsync();

        return rooms
            .Where(r => currentRoomId is null || r.Id != currentRoomId.Value)
            .Where(r => IsGenderMatch(r.Building.Gender, studentGender))
            .Where(r => r.Status is RoomStatus.Available or RoomStatus.Occupied)
            .Select(r => new { Room = r, Remaining = r.Building.StandardRoomCapacity - GetCurrentOccupancy(r) })
            .Where(x => x.Remaining >= neededCapacity)
            .OrderBy(x => x.Room.Building.Name).ThenBy(x => x.Room.Floor).ThenBy(x => x.Room.RoomNumber)
            .Select(x => new CandidateRoomDto
            {
                RoomId = x.Room.Id,
                RoomNumber = x.Room.RoomNumber,
                Floor = x.Room.Floor,
                BuildingId = x.Room.BuildingId,
                BuildingName = x.Room.Building.Name,
                RemainingCapacity = x.Remaining
            })
            .ToList();
    }

    public async Task<AllocationDto> CreateAsync(CreateAllocationDto dto)
    {
        var (studentGender, neededCapacity, request, group) = await ResolveTargetAsync(dto.HousingRequestId, dto.HousingGroupId);

        var room = await _roomRepository.GetByIdWithOccupantsAsync(dto.RoomId);
        if (room is null)
        {
            throw new ArgumentException("Room was not found.");
        }

        if (!IsGenderMatch(room.Building.Gender, studentGender))
        {
            throw new ArgumentException("This room's building does not match the required gender.");
        }

        if (room.Status is not (RoomStatus.Available or RoomStatus.Occupied))
        {
            throw new ArgumentException("This room is not available for allocation.");
        }

        var occupancyBefore = GetCurrentOccupancy(room);
        if (room.Building.StandardRoomCapacity - occupancyBefore < neededCapacity)
        {
            throw new ArgumentException("This room does not have enough remaining capacity.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var allocation = new Allocation
        {
            HousingRequestId = dto.HousingRequestId,
            HousingGroupId = dto.HousingGroupId,
            RoomId = dto.RoomId,
            AllocatedAt = now,
            CreatedAt = now
        };

        await _allocationRepository.AddAsync(allocation);

        // Compute from the occupancy captured *before* AddAsync — EF relationship fixup can add
        // the new row to room.Allocations immediately, so re-counting here would double-count it.
        var newOccupancy = occupancyBefore + neededCapacity;
        room.Status = newOccupancy >= room.Building.StandardRoomCapacity ? RoomStatus.Full : RoomStatus.Occupied;
        room.UpdatedAt = now;
        _roomRepository.Update(room);

        List<string> occupantStudentIds;
        if (request is not null)
        {
            occupantStudentIds = [request.StudentId];
        }
        else
        {
            group!.Status = HousingGroupStatus.Allocated;
            group.AllocatedAt = now;
            group.UpdatedAt = now;
            _groupRepository.Update(group);
            occupantStudentIds = group.Members.Select(m => m.StudentId).ToList();
        }

        await _allocationRepository.SaveChangesAsync();

        await _notificationPublisher.NotifyUsersAsync(
            occupantStudentIds,
            "تم تخصيص سكنك",
            $"تم تخصيص غرفة {room.RoomNumber} في مبنى {room.Building.Name} لك.");

        return MapToDto(allocation, room, occupantStudentIds);
    }

    public async Task<AutoAssignResultDto> AutoAssignAsync(AutoAssignRequestDto dto)
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            throw new ArgumentException("No housing cycle is currently open.");
        }

        var result = new AutoAssignResultDto { DryRun = dto.DryRun };

        // --- Build the target list ------------------------------------------------
        // A "target" is one thing that needs a single room: an individual (1 seat) or a
        // whole group (member-count seats, always kept together in one room).
        var targets = new List<AutoAssignTarget>();

        var individuals = await _requestRepository.GetAcceptedUngroupedForCycleAsync(cycle.Id);
        foreach (var request in individuals)
        {
            if (request.Allocations.Any(a => a.VacatedAt is null))
            {
                continue; // already housed
            }

            targets.Add(new AutoAssignTarget
            {
                IsGroup = false,
                RequestId = request.Id,
                Size = 1,
                Gender = request.Gender,
                StudentIds = [request.StudentId],
                AcceptedAt = request.AdmissionDecision!.DecisionDate
            });
        }

        var groups = await _groupRepository.GetForCycleWithMembersDecisionsAndAllocationAsync(cycle.Id);
        foreach (var group in groups)
        {
            if (group.Allocations.Any(a => a.VacatedAt is null) || group.Members.Count == 0)
            {
                // Skip only a group that's *actively* housed (or empty). A group whose earlier
                // allocation was vacated is a valid re-housing target — same as an individual.
                continue;
            }

            if (group.Members.Any(m => m.AdmissionDecision?.Status != AdmissionDecisionStatus.Accepted))
            {
                result.Skipped.Add(new AutoAssignSkippedDto
                {
                    TargetType = "group",
                    TargetId = group.Id,
                    Size = group.Members.Count,
                    Reason = "Not all group members have been accepted yet."
                });
                continue;
            }

            var leader = group.Members.FirstOrDefault(m => m.StudentId == group.LeaderId) ?? group.Members.First();
            targets.Add(new AutoAssignTarget
            {
                IsGroup = true,
                GroupId = group.Id,
                Size = group.Members.Count,
                Gender = leader.Gender,
                StudentIds = group.Members.Select(m => m.StudentId).ToList(),
                // The group only becomes fully eligible once its last member is accepted.
                AcceptedAt = group.Members.Max(m => m.AdmissionDecision!.DecisionDate)
            });
        }

        // --- Room pool: everything that currently has at least one free bed --------
        var rooms = (await _roomRepository.GetAllWithOccupantsAsync())
            .Where(r => r.Status is RoomStatus.Available or RoomStatus.Occupied)
            .Select(r => new AutoAssignRoom(r, r.Building.StandardRoomCapacity - GetCurrentOccupancy(r)))
            .Where(r => r.Remaining > 0)
            .ToList();

        // --- Pack: groups first (largest first, then oldest-accepted), then individuals ----
        // Best-Fit Decreasing — each target goes into the tightest room that still fits it,
        // leaving the roomier rooms available for whatever bigger target comes next.
        var ordered = targets
            .OrderByDescending(t => t.IsGroup)
            .ThenByDescending(t => t.Size)
            .ThenBy(t => t.AcceptedAt)
            .ThenBy(t => t.GroupId ?? t.RequestId ?? 0)
            .ToList();

        foreach (var target in ordered)
        {
            var room = rooms
                .Where(r => IsGenderMatch(r.Room.Building.Gender, target.Gender) && r.Remaining >= target.Size)
                .OrderBy(r => r.Remaining)
                .ThenBy(r => r.Room.Building.Name)
                .ThenBy(r => r.Room.Floor)
                .ThenBy(r => r.Room.RoomNumber)
                .FirstOrDefault();

            if (room is null)
            {
                result.Skipped.Add(new AutoAssignSkippedDto
                {
                    TargetType = target.IsGroup ? "group" : "individual",
                    TargetId = target.GroupId ?? target.RequestId!.Value,
                    Size = target.Size,
                    Reason = target.IsGroup
                        ? $"No available room has {target.Size} free beds together for the group."
                        : "No available room has a free bed."
                });
                continue;
            }

            room.Remaining -= target.Size;
            result.Assignments.Add(new AutoAssignmentDto
            {
                HousingRequestId = target.RequestId,
                HousingGroupId = target.GroupId,
                Size = target.Size,
                RoomId = room.Room.Id,
                RoomNumber = room.Room.RoomNumber,
                BuildingId = room.Room.BuildingId,
                BuildingName = room.Room.Building.Name,
                StudentIds = target.StudentIds
            });
        }

        // --- Commit (unless this is a dry run) -----------------------------------
        // Re-run every rule per placement via CreateAsync (another admin may have taken a room
        // between planning and now); anything that fails the re-check is reported, not fatal.
        if (!dto.DryRun && result.Assignments.Count > 0)
        {
            var committed = new List<AutoAssignmentDto>();
            foreach (var assignment in result.Assignments)
            {
                try
                {
                    await CreateAsync(new CreateAllocationDto
                    {
                        HousingRequestId = assignment.HousingRequestId,
                        HousingGroupId = assignment.HousingGroupId,
                        RoomId = assignment.RoomId
                    });
                    committed.Add(assignment);
                }
                catch (ArgumentException ex)
                {
                    result.Skipped.Add(new AutoAssignSkippedDto
                    {
                        TargetType = assignment.HousingGroupId is not null ? "group" : "individual",
                        TargetId = assignment.HousingGroupId ?? assignment.HousingRequestId!.Value,
                        Size = assignment.Size,
                        Reason = $"Rejected at commit: {ex.Message}"
                    });
                }
            }

            result.Assignments = committed;
        }

        result.PlacedTargets = result.Assignments.Count;
        result.HousedStudents = result.Assignments.Sum(a => a.Size);
        result.SkippedTargets = result.Skipped.Count;
        return result;
    }

    private sealed class AutoAssignTarget
    {
        public bool IsGroup { get; init; }
        public int? RequestId { get; init; }
        public int? GroupId { get; init; }
        public int Size { get; init; }
        public Gender Gender { get; init; }
        public List<string> StudentIds { get; init; } = [];
        public DateTime AcceptedAt { get; init; }
    }

    private sealed class AutoAssignRoom(Room room, int remaining)
    {
        public Room Room { get; } = room;
        public int Remaining { get; set; } = remaining;
    }

    public async Task<AllocationDto?> TransferAsync(int allocationId, TransferAllocationDto dto)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(allocationId);
        if (allocation is null)
        {
            return null;
        }

        if (allocation.VacatedAt is not null)
        {
            throw new ArgumentException("This allocation has already been vacated and can no longer be transferred.");
        }

        if (dto.NewRoomId == allocation.RoomId)
        {
            throw new ArgumentException("The student is already assigned to this room.");
        }

        Gender occupantGender;
        int neededCapacity;
        List<string> occupantStudentIds;

        if (allocation.HousingRequest is not null)
        {
            occupantGender = allocation.HousingRequest.Gender;
            neededCapacity = 1;
            occupantStudentIds = [allocation.HousingRequest.StudentId];
        }
        else if (allocation.HousingGroup is not null)
        {
            var group = allocation.HousingGroup;
            occupantGender = group.Members.First(m => m.StudentId == group.LeaderId).Gender;
            neededCapacity = group.Members.Count;
            occupantStudentIds = group.Members.Select(m => m.StudentId).ToList();
        }
        else
        {
            throw new InvalidOperationException("Allocation has neither a housing request nor a group.");
        }

        var newRoom = await _roomRepository.GetByIdWithOccupantsAsync(dto.NewRoomId);
        if (newRoom is null)
        {
            throw new ArgumentException("Room was not found.");
        }

        if (!IsGenderMatch(newRoom.Building.Gender, occupantGender))
        {
            throw new ArgumentException("This room's building does not match the required gender.");
        }

        if (newRoom.Status is not (RoomStatus.Available or RoomStatus.Occupied))
        {
            throw new ArgumentException("This room is not available for allocation.");
        }

        var newRoomRemaining = newRoom.Building.StandardRoomCapacity - GetCurrentOccupancy(newRoom);
        if (newRoomRemaining < neededCapacity)
        {
            throw new ArgumentException("This room does not have enough remaining capacity.");
        }

        var oldRoom = await _roomRepository.GetByIdWithOccupantsAsync(allocation.RoomId);
        if (oldRoom is null)
        {
            throw new InvalidOperationException("The previously allocated room could not be found.");
        }

        // Occupancy for both rooms is computed here (before RoomId changes) so the arithmetic
        // below doesn't depend on whether EF's in-memory relationship fixup has run yet.
        var oldRoomOccupancyAfterMove = GetCurrentOccupancy(oldRoom) - neededCapacity;
        var newRoomOccupancyAfterMove = GetCurrentOccupancy(newRoom) + neededCapacity;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Updated in place (unlike building evacuation, which stamps VacatedAt to preserve
        // history) — a group may have only one *active* Allocation row (filtered unique index
        // on HousingGroupId WHERE VacatedAt IS NULL), so vacate-and-recreate within one call
        // isn't an option here; an admin-corrected room assignment isn't a residency-ending
        // event anyway.
        allocation.RoomId = dto.NewRoomId;
        allocation.UpdatedAt = now;
        _allocationRepository.Update(allocation);

        oldRoom.Status = oldRoomOccupancyAfterMove switch
        {
            <= 0 => RoomStatus.Available,
            _ when oldRoomOccupancyAfterMove >= oldRoom.Building.StandardRoomCapacity => RoomStatus.Full,
            _ => RoomStatus.Occupied
        };
        oldRoom.UpdatedAt = now;
        _roomRepository.Update(oldRoom);

        newRoom.Status = newRoomOccupancyAfterMove >= newRoom.Building.StandardRoomCapacity ? RoomStatus.Full : RoomStatus.Occupied;
        newRoom.UpdatedAt = now;
        _roomRepository.Update(newRoom);

        await _allocationRepository.SaveChangesAsync();

        await _notificationPublisher.NotifyUsersAsync(
            occupantStudentIds,
            "تم نقل سكنك",
            $"تم نقلك إلى غرفة {newRoom.RoomNumber} في مبنى {newRoom.Building.Name}.");

        return MapToDto(allocation, newRoom, occupantStudentIds);
    }

    public async Task<AllocationDto?> VacateAsync(int allocationId, VacateAllocationDto dto)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(allocationId);
        if (allocation is null)
        {
            return null;
        }

        if (allocation.VacatedAt is not null)
        {
            throw new ArgumentException("This allocation has already been vacated.");
        }

        var occupantStudentIds = GetOccupantIds(allocation);

        var room = await _roomRepository.GetByIdWithOccupantsAsync(allocation.RoomId);
        if (room is null)
        {
            throw new InvalidOperationException("The allocated room could not be found.");
        }

        // Same occupancy-before-mutation approach as TransferAsync: compute what's left once
        // this allocation's occupant(s) are removed, without depending on EF navigation fixup.
        var remainingOccupancy = GetCurrentOccupancy(room) - occupantStudentIds.Count;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        allocation.VacatedAt = now;
        allocation.UpdatedAt = now;
        _allocationRepository.Update(allocation);

        // Mirrors BuildingEvacuationService: vacating doesn't touch HousingGroup.Status (it stays
        // Allocated) — there's no "group is done" transition modeled yet, same as whole-building evacuation.
        room.Status = remainingOccupancy switch
        {
            <= 0 => RoomStatus.Available,
            _ when remainingOccupancy >= room.Building.StandardRoomCapacity => RoomStatus.Full,
            _ => RoomStatus.Occupied
        };
        room.UpdatedAt = now;
        _roomRepository.Update(room);

        await _allocationRepository.SaveChangesAsync();

        if (occupantStudentIds.Count > 0)
        {
            var message = string.IsNullOrWhiteSpace(dto.Message)
                ? $"تم إخراجك من غرفة {room.RoomNumber} في مبنى {room.Building.Name}."
                : dto.Message.Trim();

            await _notificationPublisher.NotifyUsersAsync(occupantStudentIds, "تم إلغاء تخصيص سكنك", message);
        }

        return MapToDto(allocation, room, occupantStudentIds);
    }

    public async Task<AllocationDto?> RemoveGroupMemberAsync(int allocationId, string studentId)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(allocationId);
        if (allocation is null)
        {
            return null;
        }

        if (allocation.VacatedAt is not null)
        {
            throw new ArgumentException("This allocation has already been vacated.");
        }

        if (allocation.HousingGroup is null)
        {
            throw new ArgumentException("This allocation is for an individual student, not a group. Use the vacate endpoint to remove them instead.");
        }

        var group = allocation.HousingGroup;
        if (!group.Members.Any(m => m.StudentId == studentId))
        {
            throw new ArgumentException("This student is not a member of the allocated group.");
        }

        var room = await _roomRepository.GetByIdWithOccupantsAsync(allocation.RoomId);
        if (room is null)
        {
            throw new InvalidOperationException("The allocated room could not be found.");
        }

        var roomNumber = room.RoomNumber;
        var buildingName = room.Building.Name;

        // Reuses the existing leave/remove routine: it now keeps the room/allocation state in
        // sync itself (recomputes Room.Status, and vacates the allocation if this was the last
        // member), transfers leadership if needed, and notifies the remaining members.
        await _groupService.RemoveMemberAsync(group.Id, studentId);

        await _notificationPublisher.NotifyUserAsync(
            studentId,
            "تم إخراجك من الغرفة",
            $"تم إخراجك من غرفة {roomNumber} في مبنى {buildingName}.");

        var refreshed = await _allocationRepository.GetByIdWithDetailsAsync(allocationId);
        var refreshedRoom = refreshed is null ? null : await _roomRepository.GetByIdWithOccupantsAsync(refreshed.RoomId);
        return refreshed is null || refreshedRoom is null ? null : MapToDto(refreshed, refreshedRoom, GetOccupantIds(refreshed));
    }

    public async Task<IReadOnlyList<AllocationDto>> GetHistoryForStudentAsync(string studentId)
    {
        var allocations = await _allocationRepository.GetHistoryByStudentIdAsync(studentId);
        return allocations.Select(a => MapToDto(a, a.Room, GetOccupantIds(a))).ToList();
    }

    public async Task<AllocationDto?> VacateStudentAsync(string studentId, VacateAllocationDto dto)
    {
        var allocation = await _allocationRepository.GetActiveByStudentIdAsync(studentId);
        if (allocation is null)
        {
            return null;
        }

        // Individual allocation: ending it removes exactly this student. Group allocation:
        // reuse the "one member leaves, rest stay housed (or fully vacate if they're the last
        // one)" routine — this is the same distinction VacateAsync/RemoveGroupMemberAsync
        // already make, just resolved from a student id instead of a caller-supplied allocation id.
        return allocation.HousingRequestId is not null
            ? await VacateAsync(allocation.Id, dto)
            : await RemoveGroupMemberAsync(allocation.Id, studentId);
    }

    public async Task<AllocationDto?> TransferStudentAsync(string studentId, TransferAllocationDto dto)
    {
        var allocation = await _allocationRepository.GetActiveByStudentIdAsync(studentId);
        if (allocation is null)
        {
            return null;
        }

        // Individual allocation, or a group of exactly this one student: moving the whole
        // allocation and moving just this student are the same operation.
        if (allocation.HousingRequestId is not null || allocation.HousingGroup!.Members.Count == 1)
        {
            return await TransferAsync(allocation.Id, dto);
        }

        return await SplitStudentToNewRoomAsync(allocation, studentId, dto);
    }

    /// <summary>Pulls one grouped student (who has roommates remaining) out of the shared allocation and gives them their own new individual allocation in a different room.</summary>
    private async Task<AllocationDto> SplitStudentToNewRoomAsync(Allocation groupAllocation, string studentId, TransferAllocationDto dto)
    {
        var group = groupAllocation.HousingGroup!;
        var memberRequest = group.Members.First(m => m.StudentId == studentId);

        if (dto.NewRoomId == groupAllocation.RoomId)
        {
            throw new ArgumentException("The student is already assigned to this room.");
        }

        var newRoom = await _roomRepository.GetByIdWithOccupantsAsync(dto.NewRoomId);
        if (newRoom is null)
        {
            throw new ArgumentException("Room was not found.");
        }

        if (!IsGenderMatch(newRoom.Building.Gender, memberRequest.Gender))
        {
            throw new ArgumentException("This room's building does not match the required gender.");
        }

        if (newRoom.Status is not (RoomStatus.Available or RoomStatus.Occupied))
        {
            throw new ArgumentException("This room is not available for allocation.");
        }

        var remaining = newRoom.Building.StandardRoomCapacity - GetCurrentOccupancy(newRoom);
        if (remaining < 1)
        {
            throw new ArgumentException("This room does not have enough remaining capacity.");
        }

        // Leaving the group frees their old seat and recomputes that room's status via the
        // shared removal routine (also handles leadership transfer / notifying who's left behind).
        await _groupService.RemoveMemberAsync(group.Id, studentId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var newAllocation = new Allocation
        {
            HousingRequestId = memberRequest.Id,
            RoomId = dto.NewRoomId,
            AllocatedAt = now,
            CreatedAt = now
        };
        await _allocationRepository.AddAsync(newAllocation);

        var newRoomOccupancyAfterMove = GetCurrentOccupancy(newRoom) + 1;
        newRoom.Status = newRoomOccupancyAfterMove >= newRoom.Building.StandardRoomCapacity ? RoomStatus.Full : RoomStatus.Occupied;
        newRoom.UpdatedAt = now;
        _roomRepository.Update(newRoom);

        await _allocationRepository.SaveChangesAsync();

        await _notificationPublisher.NotifyUserAsync(
            studentId,
            "تم نقل سكنك",
            $"تم نقلك إلى غرفة {newRoom.RoomNumber} في مبنى {newRoom.Building.Name}.");

        return MapToDto(newAllocation, newRoom, [studentId]);
    }

    public async Task<AllocationDto?> GetByIdAsync(int id)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(id);
        return allocation is null ? null : MapToDto(allocation, allocation.Room, GetOccupantIds(allocation));
    }

    public async Task<PagedResult<AllocationDto>> GetAllAsync(int? buildingId, int? roomId, PaginationParams pagination)
    {
        var (allocations, totalCount) = await _allocationRepository.GetAllWithDetailsAsync(buildingId, roomId, pagination);
        return new PagedResult<AllocationDto>
        {
            Items = allocations.Select(a => MapToDto(a, a.Room, GetOccupantIds(a))).ToList(),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AllocationDto?> GetMineAsync(string studentId)
    {
        var cycle = await _cycleRepository.GetOpenAsync();
        if (cycle is null)
        {
            return null;
        }

        var request = await _requestRepository.GetByStudentAndCycleAsync(studentId, cycle.Id);
        if (request is null)
        {
            return null;
        }

        var allocation = await _allocationRepository.GetByHousingRequestIdAsync(request.Id);
        if (allocation is null && request.HousingGroupId is not null)
        {
            allocation = await _allocationRepository.GetByGroupIdAsync(request.HousingGroupId.Value);
        }

        return allocation is null ? null : MapToDto(allocation, allocation.Room, GetOccupantIds(allocation));
    }

    /// <param name="allowExistingAllocation">
    /// When true, an already-housed target is not rejected. Used by the candidate-rooms listing
    /// so an admin can pull a valid room list for a <c>transfer</c>; the actual allocation
    /// commit (<see cref="CreateAsync"/>) still forbids a second allocation.
    /// </param>
    private async Task<(Gender StudentGender, int NeededCapacity, HousingRequest? Request, HousingGroup? Group)> ResolveTargetAsync(int? housingRequestId, int? housingGroupId, bool allowExistingAllocation = false)
    {
        if (housingRequestId is null == housingGroupId is null)
        {
            throw new ArgumentException("Exactly one of HousingRequestId or HousingGroupId must be provided.");
        }

        if (housingRequestId is not null)
        {
            var request = await _requestRepository.GetByIdWithDocumentsAsync(housingRequestId.Value);
            if (request is null)
            {
                throw new ArgumentException("Housing request was not found.");
            }

            if (request.HousingGroupId is not null)
            {
                throw new ArgumentException("This request belongs to a housing group; allocate the group instead.");
            }

            if (request.AdmissionDecision?.Status != AdmissionDecisionStatus.Accepted)
            {
                throw new ArgumentException("This request has not been accepted.");
            }

            if (!allowExistingAllocation)
            {
                var existingAllocation = await _allocationRepository.GetByHousingRequestIdAsync(request.Id);
                if (existingAllocation is not null)
                {
                    throw new ArgumentException("This request already has an allocation.");
                }
            }

            return (request.Gender, 1, request, null);
        }

        var group = await _groupRepository.GetByIdWithMembersAndDecisionsAsync(housingGroupId!.Value);
        if (group is null)
        {
            throw new ArgumentException("Housing group was not found.");
        }

        if (!allowExistingAllocation && group.Allocations.Any(a => a.VacatedAt is null))
        {
            throw new ArgumentException("This group already has an allocation.");
        }

        if (group.Members.Count == 0)
        {
            throw new ArgumentException("This group has no members.");
        }

        if (group.Members.Any(m => m.AdmissionDecision?.Status != AdmissionDecisionStatus.Accepted))
        {
            throw new ArgumentException("Not all group members have been accepted yet.");
        }

        var leaderRequest = group.Members.First(m => m.StudentId == group.LeaderId);
        return (leaderRequest.Gender, group.Members.Count, null, group);
    }

    private static bool IsGenderMatch(Gender buildingGender, Gender studentGender) =>
        buildingGender == Gender.Mixed || buildingGender == studentGender;

    private static int GetCurrentOccupancy(Room room)
    {
        var count = 0;
        foreach (var allocation in room.Allocations.Where(a => a.VacatedAt is null))
        {
            if (allocation.HousingRequestId is not null)
            {
                count += 1;
            }
            else if (allocation.HousingGroup is not null)
            {
                count += allocation.HousingGroup.Members.Count;
            }
        }

        return count;
    }

    private static List<string> GetOccupantIds(Allocation allocation)
    {
        if (allocation.HousingRequest is not null)
        {
            return [allocation.HousingRequest.StudentId];
        }

        if (allocation.HousingGroup is not null)
        {
            return allocation.HousingGroup.Members.Select(m => m.StudentId).ToList();
        }

        return [];
    }

    private static AllocationDto MapToDto(Allocation allocation, Room room, List<string> occupantStudentIds)
    {
        return new AllocationDto
        {
            Id = allocation.Id,
            HousingRequestId = allocation.HousingRequestId,
            HousingGroupId = allocation.HousingGroupId,
            RoomId = allocation.RoomId,
            RoomNumber = room.RoomNumber,
            BuildingId = room.BuildingId,
            BuildingName = room.Building.Name,
            OccupantStudentIds = occupantStudentIds,
            AllocatedAt = allocation.AllocatedAt,
            VacatedAt = allocation.VacatedAt
        };
    }
}
