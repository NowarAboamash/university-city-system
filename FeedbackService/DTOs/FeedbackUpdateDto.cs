using FeedbackService.Enums;
using System.ComponentModel.DataAnnotations;

namespace FeedbackService.DTOs
{
    public class FeedbackUpdateDto
    {
        [Required]
        public FeedbackType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public bool IsAnonymous { get; set; }
        public bool IsRead { get; set; }
    }
}
