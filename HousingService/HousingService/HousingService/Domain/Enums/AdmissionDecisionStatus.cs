namespace HousingService.Domain.Enums;

/// <summary>
/// Represents the admission decision result.
/// </summary>
public enum AdmissionDecisionStatus
{
    Pending = 0,
    Accepted = 1,
    WaitingList = 2,
    Rejected = 3
}
