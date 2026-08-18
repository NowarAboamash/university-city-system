namespace SharedKernel.Notifications
{
    // The "public function any service can use" building block — call this instead of
    // knowing anything about NotificationService's HTTP contract. Every method is safe
    // to call and await without try/catch: a delivery failure is logged, never thrown,
    // so notifying students never breaks the caller's own operation.
    public interface INotificationPublisher
    {
        Task NotifyUserAsync(string studentId, string title, string body, string? data = null, CancellationToken cancellationToken = default);

        Task NotifyUsersAsync(IEnumerable<string> studentIds, string title, string body, string? data = null, CancellationToken cancellationToken = default);

        Task NotifyRoleAsync(string role, string title, string body, string? data = null, CancellationToken cancellationToken = default);

        Task BroadcastAsync(string title, string body, string? data = null, CancellationToken cancellationToken = default);
    }
}
