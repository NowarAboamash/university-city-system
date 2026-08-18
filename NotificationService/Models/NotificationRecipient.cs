using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationService.Models
{
    [Table("NotificationRecipients")]
    public class NotificationRecipient
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Notification))]
        public int NotificationId { get; set; }

        public Notification? Notification { get; set; }

        [Required, MaxLength(64)]
        public string StudentId { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public bool DeliveredSuccessfully { get; set; }
    }
}
