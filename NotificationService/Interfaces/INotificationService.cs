using NotificationService.DTOs;

namespace NotificationService.Interfaces
{
    public interface INotificationService
    {
        Task<PagedResult<NotificationReadDto>> GetAllAsync(PaginationParams parameters);

        Task<NotificationReadDto?> GetByIdAsync(int id);

        Task<(NotificationReadDto? Notification, string? ErrorMessage)> CreateAndSendAsync(NotificationCreateDto dto, string createdBy);

        Task<bool> UpdateAsync(int id, NotificationUpdateDto dto);

        Task<bool> DeleteAsync(int id);

        Task<PagedResult<NotificationInboxItemDto>> GetInboxAsync(string studentId, PaginationParams parameters);

        Task<bool> MarkAsReadAsync(int notificationId, string studentId);
    }
}
