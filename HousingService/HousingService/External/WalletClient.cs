using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HousingService.External;

public sealed class WalletChargeResult
{
    public bool Success { get; init; }
    public bool InsufficientBalance { get; init; }
    public decimal? NewBalance { get; init; }
}

/// <summary>
/// Deducts money from a student's wallet balance held by AuthService. Server-to-server only,
/// authenticated with the shared <c>X-Internal-Api-Key</c> (same key used for user lookup).
/// </summary>
public interface IWalletClient
{
    /// <param name="reference">Idempotency/audit reference, e.g. <c>housing-request-482</c>.</param>
    /// <returns>Success with the new balance on HTTP 200, InsufficientBalance on HTTP 402.</returns>
    /// <exception cref="InvalidOperationException">Any other response (misconfig, user missing, 5xx).</exception>
    Task<WalletChargeResult> ChargeAsync(string userId, decimal amount, string reference, string description, CancellationToken cancellationToken = default);
}

public sealed class AuthWalletClient : IWalletClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthWalletClient> _logger;

    public AuthWalletClient(HttpClient httpClient, ILogger<AuthWalletClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WalletChargeResult> ChargeAsync(string userId, decimal amount, string reference, string description, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/internal/wallet/charge", new
        {
            userId,
            amount,
            reference,
            description
        }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            decimal? balance = body.TryGetProperty("data", out var data) && data.TryGetProperty("balance", out var b)
                ? b.GetDecimal()
                : null;
            return new WalletChargeResult { Success = true, NewBalance = balance };
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            return new WalletChargeResult { Success = false, InsufficientBalance = true };
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Wallet charge failed for {Reference}: {Status} {Body}", reference, (int)response.StatusCode, errorBody);
        throw new InvalidOperationException($"Wallet charge failed with status {(int)response.StatusCode}.");
    }
}

public static class WalletClientServiceCollectionExtensions
{
    private const string BaseUrlEnvironmentVariableName = "AUTH_SERVICE_BASE_URL";
    private const string BaseUrlConfigurationKey = "AuthService:BaseUrl";
    private const string ApiKeyEnvironmentVariableName = "AUTH_SERVICE_INTERNAL_API_KEY";
    private const string ApiKeyConfigurationKey = "AuthService:InternalApiKey";

    /// <summary>
    /// Registers <see cref="IWalletClient"/> as a typed HttpClient pointed at AuthService,
    /// resolving base URL / internal API key from environment first, then configuration —
    /// the same convention as <c>AddAuthServiceUserLookup</c>.
    /// </summary>
    public static IServiceCollection AddHousingWalletClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = configuration[BaseUrlConfigurationKey];
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"AuthService base URL is not configured. Set the '{BaseUrlEnvironmentVariableName}' " +
                $"environment variable or '{BaseUrlConfigurationKey}' in configuration.");
        }

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = configuration[ApiKeyConfigurationKey];
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"AuthService internal API key is not configured. Set the '{ApiKeyEnvironmentVariableName}' " +
                $"environment variable or '{ApiKeyConfigurationKey}' in configuration.");
        }

        services.AddHttpClient<IWalletClient, AuthWalletClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
