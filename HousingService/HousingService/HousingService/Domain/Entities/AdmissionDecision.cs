using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents an admission decision for a housing request.
/// Answers the question: "Who deserves housing?"
/// Separate from physical allocation (where they actually live).
/// </summary>
public class AdmissionDecision
{
    public int Id { get; set; }
    public int HousingRequestId { get; set; }
    public AdmissionDecisionStatus Status { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime DecisionDate { get; set; }
    public string? ReviewedBy { get; set; } // Admin ID from AuthService
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual HousingRequest HousingRequest { get; set; } = null!;
}
