using Microsoft.AspNetCore.Http;

namespace FeedbackService.Interfaces
{
    public interface IFileHandler
    {
        bool IsValidImage(IFormFile file, out string errorMessage);
        Task<string?> SaveImageAsync(IFormFile file);
        Task<bool> DeleteImageAsync(string fileNameOrPath);
    }
}
