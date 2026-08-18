using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Models
{
    [Table("DeviceTokens")]
    public class DeviceToken
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string StudentId { get; set; } = string.Empty;

        // Cached from the JWT at registration time so role-based targeting
        // doesn't need a call back to AuthService.
        [Required, MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string FcmToken { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Platform { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
