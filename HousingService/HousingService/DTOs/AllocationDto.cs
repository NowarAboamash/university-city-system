namespace HousingService.DTOs;

public class AllocationDto
{
    public int Id { get; set; }

    public int? HousingRequestId { get; set; }

    public int? HousingGroupId { get; set; }

    public int RoomId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int BuildingId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    public List<string> OccupantStudentIds { get; set; } = [];

    public DateTime AllocatedAt { get; set; }

    public DateTime? VacatedAt { get; set; }
}
