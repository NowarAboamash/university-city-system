using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class UpdateBuildingDto
{
    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    public BuildingStatus Status { get; set; }

    public int? FloorsCount { get; set; }

    public int StandardRoomCapacity { get; set; }

    public string? Description { get; set; }
}
