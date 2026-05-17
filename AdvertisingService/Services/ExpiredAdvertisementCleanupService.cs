using AdvertisingService.Data;
using AdvertisingService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdvertisingService.Services;

public sealed class ExpiredAdvertisementCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(3);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredAdvertisementCleanupService> _logger;

    public ExpiredAdvertisementCleanupService(IServiceScopeFactory scopeFactory, ILogger<ExpiredAdvertisementCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAdvertisementsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up expired advertisement images.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupExpiredAdvertisementsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdvertisingDbContext>();
        var imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();

        var now = DateTime.UtcNow;
        var expiredAds = await dbContext.Advertisements
            .Where(ad => ad.EndDate < now && ad.IsActive)
            .ToListAsync(cancellationToken);

        if (expiredAds.Count == 0)
        {
            return;
        }

        foreach (var ad in expiredAds)
        {
            await imageStorage.DeleteAsync(ad.ImageUrl, cancellationToken);
            ad.IsActive = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Expired advertisement cleanup processed {Count} advertisements.", expiredAds.Count);
    }
}
