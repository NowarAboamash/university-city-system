using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class BuildingDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Gender Gender { get; set; }

    public BuildingStatus Status { get; set; }

    public int? FloorsCount { get; set; }

    public int StandardRoomCapacity { get; set; }

    public string? Description { get; set; }

    public int RoomsCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
