namespace NotificationService.Interfaces
{
    public sealed record AuthUserFcmInfo(string Id, string? FcmToken);

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
