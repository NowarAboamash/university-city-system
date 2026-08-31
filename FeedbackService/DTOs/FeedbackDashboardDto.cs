using FeedbackService.Enums;

namespace FeedbackService.DTOs
{
    /// <summary>Feedback/complaint tiles for the admin overview dashboard.</summary>
    public class FeedbackDashboardDto
    {
        /// <summary>Complaints with no admin reply yet.</summary>
        public int OpenComplaints { get; set; }

        /// <summary>Feedback (any type) staff hasn't opened yet.</summary>
        public int UnreadCount { get; set; }

        public int TotalComplaints { get; set; }

        public int TotalSuggestions { get; set; }

        /// <summary>Newest feedback first (up to 5), suggestions and complaints together.</summary>
        public List<FeedbackDashboardItemDto> RecentFeedback { get; set; } = [];
    }

    public class FeedbackDashboardItemDto
    {
        public int Id { get; set; }

        public FeedbackType Type { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Null for anonymous feedback (anonymity is a display rule, not a data one).</summary>
        public string? StudentName { get; set; }

        public bool IsAnonymous { get; set; }

        public bool IsRead { get; set; }

        public bool IsReplied { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
