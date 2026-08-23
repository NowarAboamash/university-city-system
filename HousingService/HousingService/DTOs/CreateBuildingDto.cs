using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class CreateBuildingDto
{
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    public int? FloorsCount { get; set; }

    public int StandardRoomCapacity { get; set; }

    public string? Description { get; set; }
}
