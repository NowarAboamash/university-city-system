namespace HousingService.DTOs;

public class BuildingLookupDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Number of floors — lets a student app render a floor picker (1..FloorsCount).
    /// May be null for older buildings; fall back to the max Floor in the rooms lookup.</summary>
    public int? FloorsCount { get; set; }
}
