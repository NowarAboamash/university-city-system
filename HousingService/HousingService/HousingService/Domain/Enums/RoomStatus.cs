namespace HousingService.Domain.Enums;

/// <summary>
/// Represents the status of a room.
/// </summary>
public enum RoomStatus
{
    Available = 0,
    Occupied = 1,
    Full = 2,
    Maintenance = 3,
    Closed = 4
}
