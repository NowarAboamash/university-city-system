using AdvertisingService.Enums;
using Microsoft.AspNetCore.Http;

namespace AdvertisingService.DTOs;

public class CreateAdvertisementDto
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IFormFile? Image { get; set; }

    public AdType Type { get; set; }

    public TargetGender TargetGender { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int Priority { get; set; }

    public List<int> CollegeIds { get; set; } = new();

    public List<int> GovernorateIds { get; set; } = new();
}
