using Microsoft.AspNetCore.Http;

namespace AdvertisingService.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAsync(IFormFile image, string subfolder, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? relativePath, CancellationToken cancellationToken = default);
}
