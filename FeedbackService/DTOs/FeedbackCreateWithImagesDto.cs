using FeedbackService.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FeedbackService.DTOs
{
    public class FeedbackCreateWithImagesDto
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public FeedbackType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public bool IsAnonymous { get; set; }

        public List<IFormFile>? Images { get; set; }
    }
}
