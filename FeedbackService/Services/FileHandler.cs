using FeedbackService.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SharedKernel.Media;

namespace FeedbackService.Services
{
    public class FileHandler : IFileHandler
    {
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 5 * 1024 * 1024;
        private const string CloudinaryFolder = "feedback";

        private readonly IWebHostEnvironment _environment;
        private readonly IImageUploader _imageUploader;

        public FileHandler(IWebHostEnvironment environment, IImageUploader imageUploader)
        {
            _environment = environment;
            _imageUploader = imageUploader;
        }

        public bool IsValidImage(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file is null || file.Length == 0)
            {
                errorMessage = "File is required and cannot be empty.";
                return false;
            }

            if (file.Length > MaxFileSize)
            {
                errorMessage = "File size must not exceed 5MB.";
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                errorMessage = "Only .jpg, .jpeg, .png, and .webp files are allowed.";
                return false;
            }

            return true;
        }

        public async Task<string?> SaveImageAsync(IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            return await _imageUploader.UploadAsync(stream, file.FileName, CloudinaryFolder);
        }

        public async Task<bool> DeleteImageAsync(string fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                return false;
            }

            // Rows created before the Cloudinary migration still hold local "/uploads/..." paths.
            if (!fileNameOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !fileNameOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return DeleteLocalFile(fileNameOrPath);
            }

            return await _imageUploader.DeleteAsync(fileNameOrPath);
        }

        private bool DeleteLocalFile(string fileNameOrPath)
        {
            var normalized = fileNameOrPath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var absolutePath = Path.Combine(webRootPath, "uploads", fileName);
            if (!File.Exists(absolutePath))
            {
                return false;
            }

            File.Delete(absolutePath);
            return true;
        }
    }
}
