using System.ComponentModel.DataAnnotations;

namespace FeedbackService.DTOs
{
    public class FeedbackReplyDto
    {
        [Required]
        public string Reply { get; set; } = string.Empty;
    }
}
