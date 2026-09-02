using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class MakeAdmissionDecisionDto
{
    public AdmissionDecisionStatus Status { get; set; }

    /// <summary>Optional, only read when <see cref="Status"/> is Rejected. Defaults to
    /// <c>AdminReview</c>. The automatic non-payment eviction sets this to <c>NonPayment</c>.</summary>
    public RejectionReason? RejectionReason { get; set; }

    public string? DecisionReason { get; set; }
}
