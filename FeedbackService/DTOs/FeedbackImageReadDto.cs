namespace FeedbackService.DTOs
{
    public class FeedbackImageReadDto
    {
        public int Id { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public int FeedbackId { get; set; }
    }
}
