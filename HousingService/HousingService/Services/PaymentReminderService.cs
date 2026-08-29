using HousingService.Data.Repositories;
using HousingService.Interfaces;
using SharedKernel.Notifications;
using System.Text.Json;

namespace HousingService.Services;

public class PaymentReminderService : IPaymentReminderService
{
    private readonly IHousingRequestRepository _requestRepository;
    private readonly IHousingSettingsRepository _settingsRepository;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;

    public PaymentReminderService(
        IHousingRequestRepository requestRepository,
        IHousingSettingsRepository settingsRepository,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _requestRepository = requestRepository;
        _settingsRepository = settingsRepository;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync();
        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;

        // "Due within ReminderDaysBefore days" including the whole threshold day itself:
        // any due date strictly before the start of the day AFTER (today + ReminderDaysBefore).
        // Using <= would also work; <cutoffExclusive keeps time-of-day components out of it.
        // Overdue requests (due date already in the past) are still caught here, so a job that
        // missed a day still sends the reminder late rather than skipping it entirely.
        var cutoffExclusive = today.AddDays(settings.ReminderDaysBefore + 1);

        var due = (await _requestRepository.GetDueForPaymentReminderAsync(cutoffExclusive)).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var request in due)
        {
            var daysLeft = (request.PaymentDueDate!.Value.Date - today).Days;
            var body = daysLeft > 0
                ? $"يستحق دفع رسوم طلب التسكين رقم {request.Id} خلال {daysLeft} يوم. يرجى الدفع من رصيدك."
                : $"انتهت مهلة دفع رسوم طلب التسكين رقم {request.Id}. يرجى الدفع فوراً.";

            await _notificationPublisher.NotifyUserAsync(
                request.StudentId,
                "تذكير بموعد دفع رسوم السكن",
                body,
                JsonSerializer.Serialize(new { type = "housing_payment_reminder", relatedId = request.Id }),
                cancellationToken);

            request.ReminderSent = true;
            _requestRepository.Update(request);
        }

        await _requestRepository.SaveChangesAsync();
        return due.Count;
    }
}
