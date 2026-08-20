using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NotificationService.Interfaces;

namespace NotificationService.Services
{
    public sealed class AuthServiceClient : IAuthServiceClient
    {
        private const int MaxIdsPerRequest = 200;

        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthServiceClient> _logger;

        public AuthServiceClient(HttpClient httpClient, ILogger<AuthServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IReadOnlyDictionary<string, AuthUserFcmInfo>> LookupUsersAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken = default)
        {
            var uniqueIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var result = new Dictionary<string, AuthUserFcmInfo>();

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
                        result[id] = new AuthUserFcmInfo(user.Id, user.FcmToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let a lookup failure break notification creation — worst case,
                // this batch's recipients just don't get a push (they still get an inbox entry).
                _logger.LogWarning(ex, "AuthService user lookup call failed");
            }

            return result;
        }

        public async Task<IReadOnlyList<AuthUserFcmInfo>> GetFcmTokensAsync(
            string? role,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(role)
                    ? "api/internal/users/fcm-tokens"
                    : $"api/internal/users/fcm-tokens?role={Uri.EscapeDataString(role)}";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AuthService fcm-tokens lookup returned {StatusCode}", response.StatusCode);
                    return [];
                }

                var payload = await response.Content.ReadFromJsonAsync<FcmTokensResponse>(cancellationToken: cancellationToken);
                if (payload?.Data is null)
                {
                    return [];
                }

                return payload.Data
                    .Where(u => !string.IsNullOrEmpty(u.FcmToken))
                    .Select(u => new AuthUserFcmInfo(u.Id, u.FcmToken))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuthService fcm-tokens lookup call failed");
                return [];
            }
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
            public string? FcmToken { get; set; }
        }

        private sealed class FcmTokensResponse
        {
            public bool Success { get; set; }
            public List<FcmTokenUser>? Data { get; set; }
        }

        private sealed class FcmTokenUser
        {
            public string Id { get; set; } = string.Empty;
            public string? FcmToken { get; set; }
        }
    }
}
