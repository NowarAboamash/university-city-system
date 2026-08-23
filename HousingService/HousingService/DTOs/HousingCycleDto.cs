using HousingService.Domain.Enums;

namespace HousingService.DTOs;

public class HousingCycleDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public HousingCycleStatus Status { get; set; }

    public DateTime? OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
