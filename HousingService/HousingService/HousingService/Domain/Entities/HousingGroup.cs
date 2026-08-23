using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a housing group (roommates) sharing one physical room, tied to a single
/// HousingCycle. Every member (including the leader) must already have their own
/// individual HousingRequest for that cycle before joining — the group links existing
/// requests together, it isn't a separate application path.
/// </summary>
public class HousingGroup
{
    public int Id { get; set; }
    public required string LeaderId { get; set; } // From AuthService (the student who created the group)
    public required string Code { get; set; } // e.g. "GRP-2026-A204" — used by others to join
    public int HousingCycleId { get; set; }
    public HousingGroupStatus Status { get; set; }
    public int MaxMembers { get; set; } = 4; // Leader + up to 3 others
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual HousingCycle HousingCycle { get; set; } = null!;
    public ICollection<HousingRequest> Members { get; set; } = [];
    public ICollection<GroupInvitation> Invitations { get; set; } = [];
    public virtual Allocation? Allocation { get; set; }
}
