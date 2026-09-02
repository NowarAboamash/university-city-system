namespace HousingService.Domain.Enums;

/// <summary>
/// Why an admission decision landed on <see cref="AdmissionDecisionStatus.Rejected"/>.
/// Lets the admin tell a manual review rejection apart from an automatic eviction for
/// non-payment. Null on any non-Rejected decision.
/// </summary>
public enum RejectionReason
{
    /// <summary>Rejected by an admin during application review.</summary>
    AdminReview = 0,

    /// <summary>Auto-rejected because the housing fee wasn't paid within the deadline.
    /// The student's room (if any) is freed and they're dropped from any group; getting
    /// housing again means applying afresh in a future cycle.</summary>
    NonPayment = 1
}
