namespace HousingService.DTOs;

public class CreateAllocationDto
{
    public int? HousingRequestId { get; set; }

    public int? HousingGroupId { get; set; }

    public int RoomId { get; set; }
}
