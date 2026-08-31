namespace HousingService.DTOs;

/// <summary>
/// Minimal room info any authenticated role can read — e.g. so a student's app can let them
/// pick their previous floor/room when submitting a housing request. Deliberately excludes
/// status and occupant ids (the full admin <see cref="RoomDto"/> keeps those).
/// </summary>
public class RoomLookupDto
{
    public int Id { get; set; }

    public int Floor { get; set; }

    public string RoomNumber { get; set; } = string.Empty;
}
