using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a join request for a housing group. Joining is pull-only: a student enters
/// the group's code themselves (InvitedByStudentId == InvitedStudentId in this flow — there
/// is no leader-initiated push invite), which creates a Pending row the leader must approve.
/// </summary>
public class GroupInvitation
{
    public int Id { get; set; }
    public int HousingGroupId { get; set; }
    public required string InvitedStudentId { get; set; } // From AuthService — the student joining
    public required string InvitedByStudentId { get; set; } // From AuthService — same student, self-service join
    public InvitationStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual HousingGroup HousingGroup { get; set; } = null!;
}
