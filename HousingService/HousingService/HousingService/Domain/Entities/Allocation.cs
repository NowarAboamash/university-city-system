using HousingService.Domain.Enums;

namespace HousingService.Domain.Entities;

/// <summary>
/// Represents the physical allocation of an accepted student to a room.
/// Answers the question: "Where should accepted students live?"
/// Separate from admission decision (who deserves housing).
/// </summary>
public class Allocation
{
    public int Id { get; set; }
    public int? HousingRequestId { get; set; }
    public int? HousingGroupId { get; set; }
    public int RoomId { get; set; }
    public DateTime AllocatedAt { get; set; }
    public DateTime? VacatedAt { get; set; } // Set when the building is evacuated; null means still active
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual HousingRequest? HousingRequest { get; set; }
    public virtual HousingGroup? HousingGroup { get; set; }
    public virtual Room Room { get; set; } = null!;
}
