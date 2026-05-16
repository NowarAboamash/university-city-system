using System.ComponentModel.DataAnnotations;
using AdvertisingService.Enums;

namespace AdvertisingService.Models;

public class Advertisement
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public AdType Type { get; set; }

    public TargetGender TargetGender { get; set; }

    public bool IsActive { get; set; }

    public int Priority { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<AdvertisementCollege> AdvertisementColleges { get; set; } = new List<AdvertisementCollege>();

    public ICollection<AdvertisementGovernorate> AdvertisementGovernorates { get; set; } = new List<AdvertisementGovernorate>();
}
