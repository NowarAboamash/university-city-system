using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IAllocationService
{
    Task<IReadOnlyList<CandidateRoomDto>> GetCandidateRoomsAsync(int? housingRequestId, int? housingGroupId);

    Task<AllocationDto> CreateAsync(CreateAllocationDto dto);

    Task<AllocationDto?> GetByIdAsync(int id);

    Task<PagedResult<AllocationDto>> GetAllAsync(int? buildingId, int? roomId, PaginationParams pagination);

    Task<AllocationDto?> GetMineAsync(string studentId);

    /// <returns>null if the allocation was not found. Throws ArgumentException for validation failures (already vacated, same room, gender/capacity mismatch, room not found).</returns>
    Task<AllocationDto?> TransferAsync(int allocationId, TransferAllocationDto dto);

    /// <summary>Removes a single allocation's occupant(s) from their room without assigning a new one (frees the room's capacity).</summary>
    /// <returns>null if the allocation was not found. Throws ArgumentException if already vacated.</returns>
    Task<AllocationDto?> VacateAsync(int allocationId, VacateAllocationDto dto);

    /// <summary>Removes one specific member from a group's shared room allocation, leaving the rest of the group housed there.</summary>
    /// <returns>null if the allocation was not found. Throws ArgumentException if it's an individual (non-group) allocation, already vacated, or the student isn't a member of the allocated group.</returns>
    Task<AllocationDto?> RemoveGroupMemberAsync(int allocationId, string studentId);

    /// <summary>Every allocation a student has ever had (active or vacated), individually or via a group they currently belong to, most recent first.</summary>
    Task<IReadOnlyList<AllocationDto>> GetHistoryForStudentAsync(string studentId);

    /// <summary>Removes this specific student from wherever they currently live, without needing to know their allocation id or whether they're individually or group-housed. Delegates to VacateAsync (individual, or a group's last member) or RemoveGroupMemberAsync (a grouped student with roommates remaining).</summary>
    /// <returns>null if the student has no active allocation. Throws ArgumentException if already vacated (race condition).</returns>
    Task<AllocationDto?> VacateStudentAsync(string studentId, VacateAllocationDto dto);

    /// <summary>Moves this specific student to a different room. If they're an individual (or their group's only member), moves the whole allocation like TransferAsync. If they have roommates, splits them off into their own new individual allocation in the target room, leaving the rest of the group housed where they were.</summary>
    /// <returns>null if the student has no active allocation. Throws ArgumentException for the same validation failures as TransferAsync.</returns>
    Task<AllocationDto?> TransferStudentAsync(string studentId, TransferAllocationDto dto);
}
