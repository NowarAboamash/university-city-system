using SharedKernel.Media;

namespace HousingService.Tests.Fakes;

/// <summary>No real Cloudinary call — just returns a deterministic fake path and records deletions.</summary>
public class FakeImageUploader : IImageUploader
{
    public List<string> DeletedPaths { get; } = [];

    public Task<string> UploadAsync(Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-cloudinary.test/{folder}/{fileName}");

    public Task<bool> DeleteAsync(string secureUrl, CancellationToken cancellationToken = default)
    {
        DeletedPaths.Add(secureUrl);
        return Task.FromResult(true);
    }
}
