using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using NotificationService.Interfaces;

namespace NotificationService.Services
{
    // Credentials resolve lazily (first send), not at startup — same reasoning as
    // CloudinaryImageUploader: the service should boot even before Firebase is configured.
    public sealed class FirebasePushNotificationSender : IPushNotificationSender
    {
        private const int MaxTokensPerBatch = 500;

        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebasePushNotificationSender> _logger;
        private readonly Lazy<FirebaseMessaging> _messaging;

        public FirebasePushNotificationSender(IConfiguration configuration, ILogger<FirebasePushNotificationSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _messaging = new Lazy<FirebaseMessaging>(CreateMessaging);
        }

        public async Task<IReadOnlyCollection<string>> SendAsync(
            IReadOnlyCollection<string> fcmTokens,
            string title,
            string body,
            string? data,
            CancellationToken cancellationToken = default)
        {
            if (fcmTokens.Count == 0)
            {
                return [];
            }

            var messaging = _messaging.Value;
            var delivered = new List<string>();

            var payload = string.IsNullOrWhiteSpace(data)
                ? null
                : new Dictionary<string, string> { ["payload"] = data };

            foreach (var batch in Chunk(fcmTokens, MaxTokensPerBatch))
            {
                var message = new MulticastMessage
                {
                    Tokens = batch,
                    Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                    Data = payload
                };

                var response = await messaging.SendEachForMulticastAsync(message, cancellationToken);

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    if (response.Responses[i].IsSuccess)
                    {
                        delivered.Add(batch[i]);
                    }
                    else
                    {
                        _logger.LogWarning(
                            response.Responses[i].Exception,
                            "FCM delivery failed for token ending in ...{TokenSuffix}: {Reason}",
                            batch[i][^Math.Min(6, batch[i].Length)..],
                            response.Responses[i].Exception?.Message);
                    }
                }
            }

            return delivered;
        }

        private FirebaseMessaging CreateMessaging()
        {
            var serviceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
            if (string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                serviceAccountJson = _configuration["Firebase:ServiceAccountJson"];
            }

            if (string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                throw new InvalidOperationException(
                    "Firebase is not configured. Set the FIREBASE_SERVICE_ACCOUNT_JSON environment variable " +
                    "or Firebase:ServiceAccountJson in configuration to the full service account JSON from the Firebase console.");
            }

            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(serviceAccountJson)
            });

            return FirebaseMessaging.GetMessaging(app);
        }

        private static IEnumerable<List<string>> Chunk(IReadOnlyCollection<string> source, int size)
        {
            var list = source as IList<string> ?? source.ToList();
            for (var i = 0; i < list.Count; i += size)
            {
                yield return list.Skip(i).Take(size).ToList();
            }
        }
    }
}
