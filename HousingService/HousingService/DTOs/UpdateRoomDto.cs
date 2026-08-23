using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class UpdateRoomDto
{
    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }

    public RoomStatus Status { get; set; }
}
