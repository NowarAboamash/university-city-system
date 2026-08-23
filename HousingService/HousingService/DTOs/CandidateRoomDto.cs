namespace HousingService.DTOs;

public class CandidateRoomDto
{
    public int RoomId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }

    public int BuildingId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    public int RemainingCapacity { get; set; }
}
