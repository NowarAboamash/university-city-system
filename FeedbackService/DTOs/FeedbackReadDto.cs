using FeedbackService.Enums;

namespace FeedbackService.DTOs
{
    public class FeedbackReadDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public FeedbackType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
