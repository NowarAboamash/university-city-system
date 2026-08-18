namespace NotificationService.Interfaces
{
    public interface IDeviceTokenService
    {
        Task RegisterAsync(string studentId, string role, string fcmToken, string? platform);

        Task<bool> UnregisterAsync(string studentId, string fcmToken);

        Task<IReadOnlyList<string>> GetTokensForStudentAsync(string studentId);

        Task<IReadOnlyList<string>> GetTokensForStudentsAsync(IEnumerable<string> studentIds);

        Task<IReadOnlyList<string>> GetTokensForRoleAsync(string role);

        Task<IReadOnlyList<string>> GetAllTokensAsync();
    }
}
