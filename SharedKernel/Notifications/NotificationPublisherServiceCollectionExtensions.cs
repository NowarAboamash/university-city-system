using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Notifications
{
    public static class NotificationPublisherServiceCollectionExtensions
    {
        private const string BaseUrlEnvironmentVariableName = "NOTIFICATION_SERVICE_BASE_URL";
        private const string BaseUrlConfigurationKey = "NotificationService:BaseUrl";
        private const string ServiceKeyEnvironmentVariableName = "INTERNAL_SERVICE_KEY";
        private const string ServiceKeyConfigurationKey = "InternalApi:ServiceKey";

        // sourceServiceName identifies the calling service to NotificationService (stored as CreatedBy),
        // e.g. AddSharedNotificationPublisher(builder.Configuration, "AdvertisingService").
        public static IServiceCollection AddSharedNotificationPublisher(
            this IServiceCollection services,
            IConfiguration configuration,
            string sourceServiceName)
        {
            var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = configuration[BaseUrlConfigurationKey];
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    $"NotificationService base URL is not configured. Set the '{BaseUrlEnvironmentVariableName}' " +
                    $"environment variable or '{BaseUrlConfigurationKey}' in configuration.");
            }

            var serviceKey = Environment.GetEnvironmentVariable(ServiceKeyEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(serviceKey))
            {
                serviceKey = configuration[ServiceKeyConfigurationKey];
            }

            if (string.IsNullOrWhiteSpace(serviceKey))
            {
                throw new InvalidOperationException(
                    $"Internal service key is not configured. Set the '{ServiceKeyEnvironmentVariableName}' " +
                    $"environment variable or '{ServiceKeyConfigurationKey}' in configuration.");
            }

            services.AddSingleton(new NotificationPublisherOptions { SourceServiceName = sourceServiceName });

            services.AddHttpClient<INotificationPublisher, HttpNotificationPublisher>(client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            return services;
        }
    }
}
