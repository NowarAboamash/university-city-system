using AdvertisingService.Enums;

namespace AdvertisingService.DTOs;

public class AdvertisementDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public AdType Type { get; set; }

    public TargetGender TargetGender { get; set; }

    public int Priority { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}
