using System.ComponentModel.DataAnnotations;

namespace NotificationService.DTOs
{
    public class DeviceTokenRegisterDto
    {
        [Required, MaxLength(500)]
        public string FcmToken { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Platform { get; set; }
    }
}
