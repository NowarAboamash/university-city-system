using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class HousingGroupDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string LeaderId { get; set; } = string.Empty;

    public int HousingCycleId { get; set; }

    public HousingGroupStatus Status { get; set; }

    public int MaxMembers { get; set; }

    public List<string> MemberStudentIds { get; set; } = [];

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    // Only populated when the caller is the group's leader.
    public List<GroupInvitationDto>? PendingInvitations { get; set; }
}
