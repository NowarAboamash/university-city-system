using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class RoomDto
{
    public int Id { get; set; }

    public int BuildingId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int Floor { get; set; }

    public int Capacity { get; set; }

    public int CurrentOccupancy { get; set; }

    public RoomStatus Status { get; set; }

    public List<string> OccupantStudentIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
