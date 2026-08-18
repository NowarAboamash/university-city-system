using NotificationService.Enums;
using System.ComponentModel.DataAnnotations;

namespace NotificationService.DTOs
{
    public class NotificationCreateDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Data { get; set; }

        [Required]
        public NotificationTargetType TargetType { get; set; }

        // Required when TargetType == User (exactly one id) or Users (one or more).
        public List<string>? TargetStudentIds { get; set; }

        // Required when TargetType == Role.
        public string? TargetRole { get; set; }
    }
}
