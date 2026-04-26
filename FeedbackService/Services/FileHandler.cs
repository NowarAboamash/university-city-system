using FeedbackService.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FeedbackService.Services
{
    public class FileHandler : IFileHandler
    {
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 5 * 1024 * 1024;

        private readonly IWebHostEnvironment _environment;

        public FileHandler(IWebHostEnvironment environment)
        {
            _environment = environment;
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
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsDirectory = Path.Combine(webRootPath, "uploads");
            Directory.CreateDirectory(uploadsDirectory);

            var absolutePath = Path.Combine(uploadsDirectory, fileName);
            await using var stream = new FileStream(absolutePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{fileName}";
        }

        public Task<bool> DeleteImageAsync(string fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                return Task.FromResult(false);
            }

            var normalized = fileNameOrPath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Task.FromResult(false);
            }

            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var absolutePath = Path.Combine(webRootPath, "uploads", fileName);
            if (!File.Exists(absolutePath))
            {
                return Task.FromResult(false);
            }

            File.Delete(absolutePath);
            return Task.FromResult(true);
        }
    }
}
