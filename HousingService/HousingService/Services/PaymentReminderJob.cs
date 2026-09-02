using HousingService.Interfaces;

namespace HousingService.Services;

/// <summary>
/// Runs the daily housing-fee sweep once every 24 hours: first <see cref="IUnpaidEvictionService"/>
/// (evict Accepted+unpaid requests whose deadline day has passed), then
/// <see cref="IPaymentReminderService"/> (nudge those still within the window). The timing accuracy
/// is intentionally coarse (a day, not seconds). NotificationService itself has no scheduled/delayed
/// send, so owning the "when" here is the whole point.
/// </summary>
public sealed class PaymentReminderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentReminderJob> _logger;

    public PaymentReminderJob(IServiceScopeFactory scopeFactory, ILogger<PaymentReminderJob> logger)
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
                using var scope = _scopeFactory.CreateScope();

                var evictionService = scope.ServiceProvider.GetRequiredService<IUnpaidEvictionService>();
                var evicted = await evictionService.RunAsync(stoppingToken);
                if (evicted > 0)
                {
                    _logger.LogInformation("Payment enforcement job evicted {Count} unpaid request(s).", evicted);
                }

                var reminderService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                var notified = await reminderService.RunAsync(stoppingToken);
                if (notified > 0)
                {
                    _logger.LogInformation("Payment reminder job notified {Count} student(s).", notified);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment reminder/enforcement job run failed.");
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
}
