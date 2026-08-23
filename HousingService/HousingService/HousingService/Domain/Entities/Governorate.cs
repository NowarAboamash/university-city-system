namespace HousingService.Domain.Entities;

/// <summary>
/// A simple admin-managed lookup of governorates the platform serves.
/// Kept as a table (not an enum) so new governorates can be added without a redeploy,
/// since the platform currently serves Aleppo only but is meant to expand later.
/// </summary>
public class Governorate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
