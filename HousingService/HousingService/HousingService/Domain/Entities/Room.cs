using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents a room in a building.
/// </summary>
public class Room
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public required string RoomNumber { get; set; }
    public int Floor { get; set; }
    public int CurrentOccupancy { get; set; }
    public RoomStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Building Building { get; set; } = null!;
    public ICollection<Allocation> Allocations { get; set; } = [];
}
