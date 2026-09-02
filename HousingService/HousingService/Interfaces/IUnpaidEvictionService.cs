namespace HousingService.Interfaces;

public interface IUnpaidEvictionService
{
    /// <summary>
    /// Evicts every Accepted, still-unpaid request whose payment deadline day has fully passed:
    /// the admission decision is flipped to Rejected (reason <c>NonPayment</c>), which cascades to
    /// free any room they hold and drop them from any group, and the student is notified. There is
    /// no grace period (the deadline is already the full window) and no automatic reinstatement —
    /// a paid-too-late student must apply again in a future cycle.
    /// </summary>
    /// <returns>How many requests were evicted this run.</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
