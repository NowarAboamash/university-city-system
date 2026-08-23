using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents one annual housing season (e.g. "2026-2027") that housing requests are submitted against.
/// Freely reopenable by the admin (e.g. to admit more students into spots freed up by
/// accepted students who never finalized/paid for their spot) — not a one-way state machine.
/// </summary>
public class HousingCycle
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public HousingCycleStatus Status { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
