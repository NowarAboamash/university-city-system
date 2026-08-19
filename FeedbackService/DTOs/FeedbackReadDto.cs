using FeedbackService.Enums;

namespace FeedbackService.DTOs
{
    public class FeedbackReadDto
    {
        public int Id { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string? StudentName { get; set; }
        public FeedbackType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? AdminReply { get; set; }
        public string? RepliedByAdminId { get; set; }
        public string? RepliedByAdminName { get; set; }
        public DateTime? RepliedAt { get; set; }

        public List<FeedbackImageDto> Images { get; set; } = [];
    }
}
