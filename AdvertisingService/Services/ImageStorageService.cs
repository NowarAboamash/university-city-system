using AdvertisingService.Interfaces;
using Microsoft.AspNetCore.Http;
using SharedKernel.Media;

namespace AdvertisingService.Services;

public sealed class ImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private readonly IImageUploader _imageUploader;

    public ImageStorageService(IImageUploader imageUploader)
    {
        _imageUploader = imageUploader;
    }

    public async Task<string> UploadAsync(IFormFile image, string subfolder, CancellationToken cancellationToken = default)
    {
        Validate(image);

        await using var stream = image.OpenReadStream();
        return await _imageUploader.UploadAsync(stream, image.FileName, subfolder, cancellationToken);
    }

    public async Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        await _imageUploader.DeleteAsync(relativePath, cancellationToken);
    }

    private static void Validate(IFormFile image)
    {
        if (image is null || image.Length <= 0)
        {
            throw new ArgumentException("Image file is required.");
        }

        if (image.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("Image file must not exceed 5 MB.");
        }

        var extension = Path.GetExtension(image.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Invalid image extension. Allowed extensions are .jpg, .jpeg, .png, and .webp.");
        }
    }
}
