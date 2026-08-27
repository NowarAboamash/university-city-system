namespace NotificationService.Interfaces
{
    // NotificationsEnabled is the user's own "mute push" preference (defaults to true).
    // Only the id-targeted lookup path carries a real value; the role/broadcast path
    // relies on AuthService having already filtered muted users out, so it leaves the
    // default. It gates the Firebase push only — the in-app inbox row is still written.
    public sealed record AuthUserFcmInfo(string Id, string? FcmToken, bool NotificationsEnabled = true);

    // Resolves FCM push tokens from AuthService's internal endpoints. AuthService is the
    // single source of truth for device tokens — NotificationService no longer stores its
    // own copy. Never throws: a failed call just means fewer/no recipients get a push,
    // matching the rest of this service's best-effort delivery philosophy.
    public interface IAuthServiceClient
    {
        Task<IReadOnlyDictionary<string, AuthUserFcmInfo>> LookupUsersAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AuthUserFcmInfo>> GetFcmTokensAsync(string? role, CancellationToken cancellationToken = default);
    }
}
