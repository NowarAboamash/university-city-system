using FeedbackService.DTOs;

namespace FeedbackService.Interfaces
{
    public interface IFeedbackService
    {
        Task<IReadOnlyList<FeedbackReadDto>> GetAllAsync();
        Task<FeedbackReadDto?> GetByIdAsync(int id);
        Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto);
        Task<(FeedbackReadDto? Feedback, string? ErrorMessage)> CreateWithImagesAsync(FeedbackCreateWithImagesDto dto);
        Task<bool> UpdateAsync(int id, FeedbackUpdateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
