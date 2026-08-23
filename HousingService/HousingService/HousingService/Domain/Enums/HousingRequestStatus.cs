namespace HousingService.Domain.Enums;

/// <summary>
/// Tracks whether a housing request is still editable by the student, not the
/// admission outcome itself (that lives entirely in <see cref="AdmissionDecisionStatus"/>).
/// </summary>
public enum HousingRequestStatus
{
    /// <summary>Submitted, awaiting admin review. Editable by the student.</summary>
    Submitted = 0,

    /// <summary>An admin rejected one of the documents; the student must fix it. Editable.</summary>
    NeedsRevision = 1,

    /// <summary>An AdmissionDecision has been recorded. No more edits.</summary>
    Locked = 2
}
