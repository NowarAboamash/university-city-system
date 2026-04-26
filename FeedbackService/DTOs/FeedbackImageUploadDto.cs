using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FeedbackService.DTOs
{
    public class FeedbackImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        public int FeedbackId { get; set; }
    }
}
