namespace AdvertisingService.Models;

public class AdvertisementGovernorate
{
    public Guid AdvertisementId { get; set; }

    public int GovernorateId { get; set; }

    public Advertisement Advertisement { get; set; } = null!;
}
