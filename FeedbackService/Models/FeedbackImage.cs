using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedbackService.Models
{
    [Table("FeedbackImages")]
    public class FeedbackImage
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(500)]
        public string ImagePath { get; set;  }
        [ForeignKey("Feedback")]
        public int FeedbackId { get; set; }

        public Feedback? Feedback {  get; set; }

    }
}
