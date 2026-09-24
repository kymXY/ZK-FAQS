namespace FaqCms.Models;

public class ArticleFeedback
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public FeedbackRating Rating { get; set; }
    public string? Comment { get; set; }
    
    // Visitor info
    public string Company { get; set; } = ""; // Required
    public string? Name { get; set; } // Optional
    
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public bool IsPublic { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
