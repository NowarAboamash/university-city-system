using NotificationService.Enums;

namespace NotificationService.DTOs
{
    public class NotificationReadDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Data { get; set; }
        public NotificationTargetType TargetType { get; set; }
        public string? TargetRole { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int RecipientCount { get; set; }
        public int DeliveredCount { get; set; }
    }
}
