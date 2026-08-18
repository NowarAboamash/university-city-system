using NotificationService.Enums;
using System.ComponentModel.DataAnnotations;

namespace NotificationService.DTOs
{
    // Shape used by the service-to-service broadcast endpoint (X-Service-Key auth,
    // no user JWT) — other backend services call this directly, e.g. AdvertisingService
    // notifying students about a new ad.
    public class InternalNotificationRequestDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Body { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Data { get; set; }

        [Required]
        public NotificationTargetType TargetType { get; set; }

        public List<string>? TargetStudentIds { get; set; }

        public string? TargetRole { get; set; }

        // Which service triggered this, e.g. "AdvertisingService" — stored as CreatedBy.
        [Required, MaxLength(64)]
        public string SourceService { get; set; } = string.Empty;
    }
}
