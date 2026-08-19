using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Users
{
    internal sealed class AuthServiceUserLookupService : IUserLookupService
    {
        private const int MaxIdsPerRequest = 200;

        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthServiceUserLookupService> _logger;

        public AuthServiceUserLookupService(HttpClient httpClient, ILogger<AuthServiceUserLookupService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<string, UserInfo>> LookupUsersAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default)
        {
            var uniqueIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var result = new Dictionary<string, UserInfo>();

            if (uniqueIds.Count == 0)
            {
                return result;
            }

            try
            {
                for (var offset = 0; offset < uniqueIds.Count; offset += MaxIdsPerRequest)
                {
                    var batch = uniqueIds.Skip(offset).Take(MaxIdsPerRequest).ToList();

                    var response = await _httpClient.PostAsJsonAsync(
                        "api/internal/users/lookup",
                        new LookupRequest(batch),
                        cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("AuthService user lookup returned {StatusCode}", response.StatusCode);
                        continue;
                    }

                    var payload = await response.Content.ReadFromJsonAsync<LookupResponse>(cancellationToken: cancellationToken);
                    if (payload?.Data is null)
                    {
                        continue;
                    }

                    foreach (var (id, user) in payload.Data)
                    {
                        result[id] = new UserInfo(user.Id, user.FirstName, user.SecondName, user.Role, user.IsDeleted);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let a lookup failure break the caller's own response.
                _logger.LogWarning(ex, "AuthService user lookup call failed");
            }

            return result;
        }

        private sealed record LookupRequest([property: JsonPropertyName("ids")] List<string> Ids);

        private sealed class LookupResponse
        {
            public bool Success { get; set; }
            public Dictionary<string, LookupUser>? Data { get; set; }
        }

        private sealed class LookupUser
        {
            public string Id { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string SecondName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
        }
    }
}
