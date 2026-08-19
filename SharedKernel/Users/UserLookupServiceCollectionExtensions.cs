using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Users
{
    public static class UserLookupServiceCollectionExtensions
    {
        private const string BaseUrlEnvironmentVariableName = "AUTH_SERVICE_BASE_URL";
        private const string BaseUrlConfigurationKey = "AuthService:BaseUrl";
        private const string ApiKeyEnvironmentVariableName = "AUTH_SERVICE_INTERNAL_API_KEY";
        private const string ApiKeyConfigurationKey = "AuthService:InternalApiKey";

        public static IServiceCollection AddAuthServiceUserLookup(this IServiceCollection services, IConfiguration configuration)
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

            services.AddHttpClient<IUserLookupService, AuthServiceUserLookupService>(client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            return services;
        }
    }
}
