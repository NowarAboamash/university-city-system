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
        var (studentGender, neededCapacity, _, _) = await ResolveTargetAsync(housingRequestId, housingGroupId);

        var rooms = await _roomRepository.GetAllWithOccupantsAsync();

        return rooms
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

        var remaining = room.Building.StandardRoomCapacity - GetCurrentOccupancy(room);
        if (remaining < neededCapacity)
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

        var newOccupancy = GetCurrentOccupancy(room) + neededCapacity;
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
        // history) — a group's Allocation row is constrained to exactly one per group by a
        // unique index on HousingGroupId, so vacate-and-recreate isn't an option here; an
        // admin-corrected room assignment isn't a residency-ending event anyway.
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

        // If this is the group's last member, removing them ends this allocation's residency
        // entirely (the group itself gets deleted below) — stamp VacatedAt too, otherwise the
        // allocation would sit forever as "active" with no occupants and no group to reference.
        var isLastMember = group.Members.Count == 1;
        var remainingOccupancy = GetCurrentOccupancy(room) - 1;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (isLastMember)
        {
            allocation.VacatedAt = now;
            allocation.UpdatedAt = now;
            _allocationRepository.Update(allocation);
        }

        room.Status = remainingOccupancy switch
        {
            <= 0 => RoomStatus.Available,
            _ when remainingOccupancy >= room.Building.StandardRoomCapacity => RoomStatus.Full,
            _ => RoomStatus.Occupied
        };
        room.UpdatedAt = now;
        _roomRepository.Update(room);

        await _roomRepository.SaveChangesAsync();

        // Reuses the existing leave/remove routine: transfers leadership (or deletes the group if
        // this was the last member) and notifies the remaining members that this student left.
        await _groupService.RemoveMemberAsync(group.Id, studentId);

        await _notificationPublisher.NotifyUserAsync(
            studentId,
            "تم إخراجك من الغرفة",
            $"تم إخراجك من غرفة {room.RoomNumber} في مبنى {room.Building.Name}.");

        var refreshed = await _allocationRepository.GetByIdWithDetailsAsync(allocationId);
        return refreshed is null ? null : MapToDto(refreshed, room, GetOccupantIds(refreshed));
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

    private async Task<(Gender StudentGender, int NeededCapacity, HousingRequest? Request, HousingGroup? Group)> ResolveTargetAsync(int? housingRequestId, int? housingGroupId)
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

            var existingAllocation = await _allocationRepository.GetByHousingRequestIdAsync(request.Id);
            if (existingAllocation is not null)
            {
                throw new ArgumentException("This request already has an allocation.");
            }

            return (request.Gender, 1, request, null);
        }

        var group = await _groupRepository.GetByIdWithMembersAndDecisionsAsync(housingGroupId!.Value);
        if (group is null)
        {
            throw new ArgumentException("Housing group was not found.");
        }

        if (group.Allocation is not null)
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
