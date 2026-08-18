namespace NotificationService.Interfaces
{
    public interface IPushNotificationSender
    {
        // Returns the FCM tokens that were successfully delivered to.
        Task<IReadOnlyCollection<string>> SendAsync(
            IReadOnlyCollection<string> fcmTokens,
            string title,
            string body,
            string? data,
            CancellationToken cancellationToken = default);
    }
}
