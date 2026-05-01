namespace FeedbackService.DTOs
{
    public class FeedbackImageDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public int FeedbackId { get; set; }
    }
}
