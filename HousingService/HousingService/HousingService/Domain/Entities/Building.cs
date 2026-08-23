using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a building in the housing system.
/// </summary>
public class Building
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Gender Gender { get; set; }
    public BuildingStatus Status { get; set; }
    public int? FloorsCount { get; set; }
    public int StandardRoomCapacity { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Room> Rooms { get; set; } = [];
}
