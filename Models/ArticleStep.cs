namespace FaqCms.Models;

public class ArticleStep
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? ImageAlt { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsOptional { get; set; }
}
