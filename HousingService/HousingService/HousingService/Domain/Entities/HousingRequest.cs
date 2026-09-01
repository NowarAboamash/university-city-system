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

    // --- Payment (housing fee) ---------------------------------------------
    // The fee is only owed once a request is Accepted, so PaymentDueDate stays null until
    // the first AdmissionDecision == Accepted, at which point it's set to
    // now + HousingSettings.PaymentDeadlineDays and never moved again.
    public DateTime? PaymentDueDate { get; set; }
    // The fee amount frozen at acceptance (from HousingSettings.HousingFeeAmount at that
    // moment) — so a later settings change never alters what an already-accepted student owes.
    public decimal? FeeAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    // The amount actually charged on the successful payment (= FeeAmount at pay time). Kept
    // explicitly so financial totals reconcile with AuthService's wallet ledger exactly.
    public decimal? AmountPaid { get; set; }
    // Guards the automatic reminder against being sent more than once per request.
    public bool ReminderSent { get; set; }

    // Navigation properties
    public virtual Governorate Governorate { get; set; } = null!;
    public virtual HousingCycle HousingCycle { get; set; } = null!;
    public virtual Building? PreviousBuilding { get; set; }
    public virtual HousingGroup? HousingGroup { get; set; }
    public ICollection<HousingRequestDocument> Documents { get; set; } = [];
    public virtual AdmissionDecision? AdmissionDecision { get; set; }
    public ICollection<Allocation> Allocations { get; set; } = [];
}
