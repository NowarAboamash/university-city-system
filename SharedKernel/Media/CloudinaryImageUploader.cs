using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace SharedKernel.Media
{
    // Credentials resolve lazily (first upload/delete), not at startup, so a service
    // can boot and serve non-image endpoints before Cloudinary keys are provisioned.
    public sealed class CloudinaryImageUploader : IImageUploader
    {
        private readonly IConfiguration _configuration;
        private readonly Lazy<Cloudinary> _cloudinary;

        public CloudinaryImageUploader(IConfiguration configuration)
        {
            _configuration = configuration;
            _cloudinary = new Lazy<Cloudinary>(CreateClient);
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Folder = folder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.Value.UploadAsync(uploadParams, cancellationToken);
            if (result.Error is not null)
            {
                throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
            }

            return result.SecureUrl.ToString();
        }

        public async Task<bool> DeleteAsync(string secureUrl, CancellationToken cancellationToken = default)
        {
            var publicId = ExtractPublicId(secureUrl);
            if (string.IsNullOrWhiteSpace(publicId))
            {
                return false;
            }

            var result = await _cloudinary.Value.DestroyAsync(new DeletionParams(publicId));
            return string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase);
        }

        private Cloudinary CreateClient()
        {
            var cloudName = GetSetting("CLOUDINARY_CLOUD_NAME", "Cloudinary:CloudName");
            var apiKey = GetSetting("CLOUDINARY_API_KEY", "Cloudinary:ApiKey");
            var apiSecret = GetSetting("CLOUDINARY_API_SECRET", "Cloudinary:ApiSecret");

            if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException(
                    "Cloudinary is not configured. Set CLOUDINARY_CLOUD_NAME/CLOUDINARY_API_KEY/CLOUDINARY_API_SECRET " +
                    "environment variables, or Cloudinary:CloudName/ApiKey/ApiSecret in configuration.");
            }

            var cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
            cloudinary.Api.Secure = true;
            return cloudinary;
        }

        private string? GetSetting(string environmentVariableName, string configurationKey)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariableName);
            return string.IsNullOrWhiteSpace(fromEnvironment) ? _configuration[configurationKey] : fromEnvironment;
        }

        private static string? ExtractPublicId(string secureUrl)
        {
            const string marker = "/upload/";
            var index = secureUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var afterUpload = secureUrl[(index + marker.Length)..];
            var segments = afterUpload.Split('/');
            var startIndex = segments.Length > 0 && IsVersionSegment(segments[0]) ? 1 : 0;

            var publicIdWithExtension = string.Join('/', segments.Skip(startIndex));
            var lastDot = publicIdWithExtension.LastIndexOf('.');
            return lastDot > 0 ? publicIdWithExtension[..lastDot] : publicIdWithExtension;
        }

        private static bool IsVersionSegment(string segment) =>
            segment.Length > 1 && segment[0] == 'v' && segment[1..].All(char.IsDigit);
    }
}
