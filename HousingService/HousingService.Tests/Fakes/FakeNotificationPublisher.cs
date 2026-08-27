using SharedKernel.Notifications;

namespace HousingService.Tests.Fakes;

public record SentNotification(string? StudentId, IReadOnlyList<string>? StudentIds, string? Role, bool Broadcast, string Title, string Body, string? Data);

/// <summary>Records every notification sent instead of making an HTTP call, so tests can assert on who got notified.</summary>
public class FakeNotificationPublisher : INotificationPublisher
{
    public List<SentNotification> Sent { get; } = [];

    public Task NotifyUserAsync(string studentId, string title, string body, string? data = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentNotification(studentId, null, null, false, title, body, data));
        return Task.CompletedTask;
    }

    public Task NotifyUsersAsync(IEnumerable<string> studentIds, string title, string body, string? data = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentNotification(null, studentIds.ToList(), null, false, title, body, data));
        return Task.CompletedTask;
    }

    public Task NotifyRoleAsync(string role, string title, string body, string? data = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentNotification(null, null, role, false, title, body, data));
        return Task.CompletedTask;
    }

    public Task BroadcastAsync(string title, string body, string? data = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(new SentNotification(null, null, null, true, title, body, data));
        return Task.CompletedTask;
    }
}
