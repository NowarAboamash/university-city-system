using FeedbackService.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedbackService.Models
{
    [Table("Feedback")]
    public class Feedback
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        public FeedbackType Type { get; set; }
        [Required, MaxLength(200)]
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsAnonymous { get; set; } = false ;
        public bool IsRead { get; set; } = false ;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow ;

        public ICollection<FeedbackImage> Images { get; set; }

    }

}
