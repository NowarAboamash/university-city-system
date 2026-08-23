namespace HousingService.Domain.Enums;

/// <summary>
/// Represents the status of a group membership invitation.
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
}
