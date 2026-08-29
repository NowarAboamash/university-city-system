namespace HousingService.Interfaces;

public interface IPaymentReminderService
{
    /// <summary>
    /// Sends the automatic "housing fee due soon" notification to every accepted, unpaid request
    /// whose deadline is within <c>ReminderDaysBefore</c> days and that hasn't been reminded yet.
    /// Idempotent day-to-day via the per-request <c>ReminderSent</c> flag.
    /// </summary>
    /// <returns>How many students were notified this run.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
