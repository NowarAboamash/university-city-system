using SharedKernel.Users;

namespace HousingService.Tests.Fakes;

/// <summary>Always returns an empty lookup — mirrors how the real service behaves when AuthService is unreachable (never throws, names just come back unknown). Tests don't rely on names.</summary>
public class FakeUserLookupService : IUserLookupService
{
    public Task<IReadOnlyDictionary<string, UserInfo>> LookupUsersAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, UserInfo>>(new Dictionary<string, UserInfo>());
}
