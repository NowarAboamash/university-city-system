using AdvertisingService.Interfaces;
using Microsoft.AspNetCore.Http;

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
    private readonly IWebHostEnvironment _environment;

    public ImageStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> UploadAsync(IFormFile image, string subfolder, CancellationToken cancellationToken = default)
    {
        Validate(image);

        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(webRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await image.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{subfolder}/{fileName}".Replace("\\", "/");
    }

    public Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var normalized = relativePath.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var physicalPath = Path.GetFullPath(Path.Combine(webRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPath = Path.GetFullPath(webRoot);

        if (!physicalPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(physicalPath))
        {
            return Task.CompletedTask;
        }

        File.Delete(physicalPath);
        return Task.CompletedTask;
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
