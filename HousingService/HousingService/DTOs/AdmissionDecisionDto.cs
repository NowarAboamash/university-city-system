using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class AdmissionDecisionDto
{
    public int Id { get; set; }

    public AdmissionDecisionStatus Status { get; set; }

    /// <summary>Set only when <see cref="Status"/> is Rejected: <c>AdminReview</c> for a review
    /// rejection, <c>NonPayment</c> for an automatic eviction after the fee deadline lapsed.</summary>
    public RejectionReason? RejectionReason { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime DecisionDate { get; set; }

    public string? ReviewedBy { get; set; }
}
