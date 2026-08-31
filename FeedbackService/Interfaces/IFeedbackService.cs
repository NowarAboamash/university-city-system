using FeedbackService.DTOs;

namespace FeedbackService.Interfaces
{
    public interface IFeedbackService
    {
        Task<PagedResult<FeedbackReadDto>> GetAllAsync(PaginationParams parameters, string? studentId);
        Task<FeedbackDashboardDto> GetDashboardAsync();
        Task<FeedbackReadDto?> GetByIdAsync(int id, bool markAsRead = false);
        Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto, string studentId);
        Task<(FeedbackReadDto? Feedback, string? ErrorMessage)> CreateWithImagesAsync(FeedbackCreateWithImagesDto dto, string studentId);
        Task<bool> UpdateAsync(int id, FeedbackUpdateDto dto);
        Task<FeedbackReadDto?> ReplyAsync(int id, FeedbackReplyDto dto, string adminId);
        Task<bool> DeleteAsync(int id);
    }
}
