using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a housing request from a student. The student never picks a room —
/// they only submit this data; room selection is a separate, later Allocation step.
/// </summary>
public class HousingRequest
{
    public int Id { get; set; }
    public required string StudentId { get; set; } // From AuthService
    public Gender Gender { get; set; } // Self-declared; needed locally to enforce group gender-matching
    public int GovernorateId { get; set; }
    public AcademicLevel AcademicLevel { get; set; }
    public int HousingCycleId { get; set; }
    public required string DetailedAddress { get; set; }
    public bool HasSpecialNeeds { get; set; }
    public bool IsPreviousResident { get; set; }
    public int? PreviousBuildingId { get; set; }
    public int? PreviousFloor { get; set; }
    public string? PreviousRoomNumber { get; set; }
    public int? HousingGroupId { get; set; }
    public HousingRequestStatus Status { get; set; }
    public string? SpecialNotes { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Governorate Governorate { get; set; } = null!;
    public virtual HousingCycle HousingCycle { get; set; } = null!;
    public virtual Building? PreviousBuilding { get; set; }
    public virtual HousingGroup? HousingGroup { get; set; }
    public ICollection<HousingRequestDocument> Documents { get; set; } = [];
    public virtual AdmissionDecision? AdmissionDecision { get; set; }
    public ICollection<Allocation> Allocations { get; set; } = [];
}
