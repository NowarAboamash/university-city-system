using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Notifications
{
    internal sealed class HttpNotificationPublisher : INotificationPublisher
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpNotificationPublisher> _logger;
        private readonly string _sourceService;

        public HttpNotificationPublisher(HttpClient httpClient, NotificationPublisherOptions options, ILogger<HttpNotificationPublisher> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _sourceService = options.SourceServiceName;
        }

        public Task NotifyUserAsync(string studentId, string title, string body, string? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(NotificationTargetType.User, title, body, data, [studentId], null, cancellationToken);

        public Task NotifyUsersAsync(IEnumerable<string> studentIds, string title, string body, string? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(NotificationTargetType.Users, title, body, data, studentIds.ToArray(), null, cancellationToken);

        public Task NotifyRoleAsync(string role, string title, string body, string? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(NotificationTargetType.Role, title, body, data, null, role, cancellationToken);

        public Task BroadcastAsync(string title, string body, string? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(NotificationTargetType.Broadcast, title, body, data, null, null, cancellationToken);

        private async Task SendAsync(
            NotificationTargetType targetType,
            string title,
            string body,
            string? data,
            string[]? targetStudentIds,
            string? targetRole,
            CancellationToken cancellationToken)
        {
            try
            {
                var payload = new
                {
                    title,
                    body,
                    data,
                    targetType,
                    targetStudentIds,
                    targetRole,
                    sourceService = _sourceService
                };

                var response = await _httpClient.PostAsJsonAsync("api/notifications/broadcast", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "NotificationService rejected a notification from {SourceService}: {StatusCode}",
                        _sourceService, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                // A notification failure must never break the caller's own operation
                // (e.g. creating an ad should succeed even if NotificationService is down).
                _logger.LogWarning(ex, "Failed to publish a notification from {SourceService}", _sourceService);
            }
        }
    }
}
