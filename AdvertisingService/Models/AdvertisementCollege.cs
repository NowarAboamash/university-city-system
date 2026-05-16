namespace AdvertisingService.Models;

public class AdvertisementCollege
{
    public Guid AdvertisementId { get; set; }

    public int CollegeId { get; set; }

    public Advertisement Advertisement { get; set; } = null!;
}
