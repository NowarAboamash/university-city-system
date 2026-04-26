using System.ComponentModel.DataAnnotations;

namespace FeedbackService.DTOs
{
    public class FeedbackImageCreateDto
    {
        [Required]
        [MaxLength(500)]
        public string ImagePath { get; set; } = string.Empty;

        [Required]
        public int FeedbackId { get; set; }
    }
}
