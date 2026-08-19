namespace SharedKernel.Users
{
    public sealed record UserInfo(string Id, string FirstName, string SecondName, string Role, bool IsDeleted)
    {
        public string FullName => $"{FirstName} {SecondName}";
    }

    // Batch name lookup against AuthService's internal user directory. Never throws —
    // a failed or partial lookup just means fewer/no names come back, so callers should
    // treat a missing dictionary entry as "name unknown", not as an error.
    public interface IUserLookupService
    {
        Task<IReadOnlyDictionary<string, UserInfo>> LookupUsersAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);
    }
}
