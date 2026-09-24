namespace FaqCms.Models;

public class Article
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? VideoUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }

    // Soft-delete / Recycle Bin support — see AppDbContext's query filter.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Tags { get; set; }

    public int? ParentArticleId { get; set; }
    public Article? ParentArticle { get; set; }
    public List<Article> RelatedArticles { get; set; } = new();

    /// <summary>"auto" (default): related articles are picked by shared tags, same as
    /// always. "manual": only the articles in ManualRelatedArticles are shown, in that
    /// order — including showing none, if the list is empty on purpose.</summary>
    public string RelatedArticlesMode { get; set; } = "auto";
    public List<ArticleRelation> ManualRelatedArticles { get; set; } = new();

    public List<ArticleStep> Steps { get; set; } = new();
    public List<ArticleFeedback> Feedback { get; set; } = new();
    public List<ArticleVersion> Versions { get; set; } = new();
    public List<ArticleTag> ArticleTags { get; set; } = new();
}

/// <summary>One manually-picked related article, in editor-chosen order. Distinct from
/// the older ParentArticle/RelatedArticles pair (a single-parent relationship used
/// nowhere in the UI) — this is a genuine many-to-many with its own ordering, needed
/// because an article can be manually related to several others and vice versa.</summary>
public class ArticleRelation
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public int RelatedArticleId { get; set; }
    public Article? RelatedArticle { get; set; }

    public int SortOrder { get; set; }
}

public class ArticleVersion
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? VideoUrl { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeNotes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";

    public List<ArticleStepVersion> StepVersions { get; set; } = new();
}

public class ArticleStepVersion
{
    public int Id { get; set; }
    public int ArticleVersionId { get; set; }
    public ArticleVersion? ArticleVersion { get; set; }

    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Color { get; set; }

    // Soft-delete / Recycle Bin support — see AppDbContext's query filter.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public List<ArticleTag> ArticleTags { get; set; } = new();
}

public class ArticleTag
{
    public int ArticleId { get; set; }
    public Article? Article { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}
