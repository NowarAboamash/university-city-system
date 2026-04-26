using FeedbackService.DTOs;
using Microsoft.AspNetCore.Http;

namespace FeedbackService.Interfaces
{
    public interface IFeedbackImageService
    {
        Task<FeedbackImageReadDto?> AddAsync(FeedbackImageCreateDto dto);
        Task<(FeedbackImageReadDto? Image, string? ErrorMessage)> UploadAsync(IFormFile file, int feedbackId);
        Task<IReadOnlyList<FeedbackImageReadDto>> GetByFeedbackIdAsync(int feedbackId);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeleteByPathAsync(string fileNameOrPath);
    }
}
