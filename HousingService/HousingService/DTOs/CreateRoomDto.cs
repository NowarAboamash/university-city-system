namespace HousingService.DTOs;

public class CreateRoomDto
{
    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }
}
