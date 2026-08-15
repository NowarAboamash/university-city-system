using FeedbackService.DTOs;

namespace FeedbackService.Interfaces
{
    public interface IFeedbackService
    {
        Task<PagedResult<FeedbackReadDto>> GetAllAsync(PaginationParams parameters);
        Task<FeedbackReadDto?> GetByIdAsync(int id);
        Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto, string studentId);
        Task<(FeedbackReadDto? Feedback, string? ErrorMessage)> CreateWithImagesAsync(FeedbackCreateWithImagesDto dto, string studentId);
        Task<bool> UpdateAsync(int id, FeedbackUpdateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
