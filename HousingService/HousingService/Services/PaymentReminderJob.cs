using HousingService.Interfaces;

namespace HousingService.Services;

/// <summary>
/// Runs <see cref="IPaymentReminderService"/> once every 24 hours. The reminder's timing accuracy
/// is intentionally coarse (a day, not seconds) — all it does is call NotificationService when a
/// request's housing-fee deadline is close. NotificationService itself has no scheduled/delayed
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
                var reminderService = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                var notified = await reminderService.RunAsync(stoppingToken);
                if (notified > 0)
                {
                    _logger.LogInformation("Payment reminder job notified {Count} student(s).", notified);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment reminder job run failed.");
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
