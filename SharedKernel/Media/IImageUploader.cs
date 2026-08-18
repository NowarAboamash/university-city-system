namespace SharedKernel.Media
{
    public interface IImageUploader
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(string secureUrl, CancellationToken cancellationToken = default);
    }
}
