using System.ComponentModel.DataAnnotations;

namespace NotificationService.DTOs
{
    public class NotificationUpdateDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Data { get; set; }
    }
}
