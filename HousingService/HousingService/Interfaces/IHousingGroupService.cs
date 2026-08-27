using HousingService.DTOs;

namespace HousingService.Interfaces;

public interface IHousingGroupService
{
    Task<HousingGroupDto> CreateAsync(string leaderId, CreateHousingGroupDto dto);

    Task<HousingGroupDto?> GetMineAsync(string studentId);

    Task<HousingGroupDto?> GetByIdAsync(int id);

    Task<PagedResult<HousingGroupDto>> GetAllAsync(int? housingCycleId, PaginationParams pagination);

    /// <returns>null if the code doesn't exist, otherwise the created invitation id wrapped in a result DTO.</returns>
    Task<GroupInvitationDto?> JoinByCodeAsync(string studentId, JoinHousingGroupDto dto);

    /// <returns>null if not found/not the leader, true on success.</returns>
    Task<bool?> RespondToInvitationAsync(string leaderId, int invitationId, RespondToInvitationDto dto);

    /// <returns>false if the caller isn't currently in a group.</returns>
    Task<bool> LeaveAsync(string studentId);

    /// <returns>null if the group or member wasn't found.</returns>
    Task<bool?> RemoveMemberAsync(int groupId, string studentId);

    /// <summary>
    /// Lets the current leader remove another member from their own group for the open cycle.
    /// </summary>
    /// <returns>
    /// null if the caller isn't currently the leader of a group, or the target isn't a member;
    /// true on success. Throws <see cref="ArgumentException"/> if the leader targets themselves.
    /// </returns>
    Task<bool?> RemoveMemberAsLeaderAsync(string leaderId, string studentId);
}
