using FaqCms.Data;
using FaqCms.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FaqCms.Services;

public class ArticleService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ArticleService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // ---------- Reads ----------

    public async Task<List<Category>> GetCategoriesAsync(bool includeHidden = false)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.Categories.Where(c => c.ParentCategoryId == null);
        if (!includeHidden)
            query = query.Where(c => c.IsVisible);
        return await query
            .Include(c => c.SubCategories.OrderBy(s => s.SortOrder))
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<List<Category>> GetCategoriesWithArticlesAsync(bool includeHidden = false)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.Categories.Where(c => c.ParentCategoryId == null);
        if (!includeHidden)
            query = query.Where(c => c.IsVisible);
        return await query
            .Include(c => c.Articles.Where(a => a.IsPublished).OrderBy(a => a.SortOrder))
            .Include(c => c.SubCategories.OrderBy(s => s.SortOrder)).ThenInclude(s => s.Articles.Where(a => a.IsPublished).OrderBy(a => a.SortOrder))
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryAsync(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Categories
            .Include(c => c.SubCategories.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }

    public async Task<Article?> GetArticleAsync(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Steps.OrderBy(s => s.SortOrder))
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.RelatedArticles).ThenInclude(ra => ra.Category)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Article?> GetArticleBySlugAsync(string slug)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Steps.OrderBy(s => s.SortOrder))
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Include(a => a.RelatedArticles).ThenInclude(ra => ra.Category)
            .FirstOrDefaultAsync(a => a.Slug == slug);
    }

    public async Task<List<Article>> GetAllArticlesAsync(bool includeDrafts = false)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.Articles.Include(a => a.Category).AsQueryable();
        if (!includeDrafts)
            query = query.Where(a => a.IsPublished);
        return await query.OrderBy(a => a.CategoryId).ThenBy(a => a.SortOrder).ToListAsync();
    }

    public async Task<List<Article>> GetPublishedArticlesAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Where(a => a.IsPublished && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow))
            .OrderBy(a => a.CategoryId).ThenBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<List<Article>> GetArticlesByCategoryAsync(int categoryId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .Where(a => a.CategoryId == categoryId && a.IsPublished && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow))
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<List<Article>> GetFeaturedArticlesAsync(int count = 5)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Where(a => a.IsPublished && a.IsFeatured && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow))
            .OrderByDescending(a => a.ViewCount)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Article>> GetRecentArticlesAsync(int count = 10)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles
            .Include(a => a.Category)
            .Where(a => a.IsPublished && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow))
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>Related articles for the public article page. "manual" mode returns
    /// exactly what was picked, in the picked order — including an empty list if that's
    /// what was chosen, rather than silently falling back to automatic. "auto" mode (the
    /// default, and what every article had before this feature existed) keeps the
    /// original shared-tag ranking.</summary>
    public async Task<List<Article>> GetRelatedArticlesAsync(int articleId, int count = 5)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == articleId);

        if (article == null) return new List<Article>();

        if (article.RelatedArticlesMode == "manual")
        {
            return await db.ArticleRelations
                .Where(r => r.ArticleId == articleId)
                .OrderBy(r => r.SortOrder)
                .Include(r => r.RelatedArticle!).ThenInclude(a => a!.Category)
                .Select(r => r.RelatedArticle!)
                .Where(a => a.IsPublished && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow))
                .ToListAsync();
        }

        var tagIds = article.ArticleTags.Select(at => at.TagId).ToList();
        if (!tagIds.Any()) return new List<Article>();

        return await db.Articles
            .Include(a => a.Category)
            .Where(a => a.Id != articleId
                     && a.IsPublished
                     && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow)
                     && a.ArticleTags.Any(at => tagIds.Contains(at.TagId)))
            .OrderByDescending(a => a.ArticleTags.Count(at => tagIds.Contains(at.TagId)))
            .ThenByDescending(a => a.ViewCount)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>The five (or so) articles GetRelatedArticlesAsync would show right now
    /// in "auto" mode, for the editor's read-only preview of that mode — so switching to
    /// it isn't a leap of faith about what will actually show.</summary>
    public async Task<List<Article>> PreviewAutoRelatedArticlesAsync(int articleId, List<int> tagIds, int count = 5)
    {
        using var db = _dbFactory.CreateDbContext();
        if (!tagIds.Any()) return new List<Article>();

        return await db.Articles
            .Include(a => a.Category)
            .Where(a => a.Id != articleId
                     && a.IsPublished
                     && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow)
                     && a.ArticleTags.Any(at => tagIds.Contains(at.TagId)))
            .OrderByDescending(a => a.ArticleTags.Count(at => tagIds.Contains(at.TagId)))
            .ThenByDescending(a => a.ViewCount)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>The article's manually-picked related articles, in order, for the editor
    /// — unlike GetRelatedArticlesAsync this includes drafts and scheduled articles too,
    /// so the editor's picker always shows exactly what's been chosen.</summary>
    public async Task<List<Article>> GetManualRelatedArticlesAsync(int articleId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.ArticleRelations
            .Where(r => r.ArticleId == articleId)
            .OrderBy(r => r.SortOrder)
            .Include(r => r.RelatedArticle!).ThenInclude(a => a!.Category)
            .Select(r => r.RelatedArticle!)
            .ToListAsync();
    }

    /// <summary>Articles the editor's related-article search box can offer — every
    /// article except the one being edited (an article can't relate to itself) and,
    /// once it exists, itself. Deliberately not filtered to published-only: a still-draft
    /// article that's about to be published alongside this one is a normal thing to
    /// pre-link. Ordered by title so the list scans easily once search narrows it.</summary>
    public async Task<List<Article>> SearchArticlesForRelatingAsync(int? excludeArticleId, string query, int take = 30)
    {
        using var db = _dbFactory.CreateDbContext();
        var q = (query ?? "").Trim();

        var results = db.Articles.Include(a => a.Category).AsQueryable();
        if (excludeArticleId is not null)
            results = results.Where(a => a.Id != excludeArticleId.Value);
        if (!string.IsNullOrEmpty(q))
            results = results.Where(a => a.Title.Contains(q));

        return await results.OrderBy(a => a.Title).Take(take).ToListAsync();
    }

    /// <summary>Saves the editor's related-articles choice: the mode ("auto"/"manual")
    /// and, for manual, the exact ordered set of picks. Always rewrites the whole set
    /// rather than diffing it — the picker only ever has a handful of rows, so there's
    /// nothing to gain from a more careful merge.</summary>
    public async Task SetRelatedArticlesAsync(int articleId, string mode, List<int> orderedRelatedIds, string updatedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == articleId);
        if (article is null) return;

        article.RelatedArticlesMode = mode == "manual" ? "manual" : "auto";

        var existing = await db.ArticleRelations.Where(r => r.ArticleId == articleId).ToListAsync();
        db.ArticleRelations.RemoveRange(existing);

        var order = 0;
        foreach (var relatedId in orderedRelatedIds.Distinct())
        {
            if (relatedId == articleId) continue; // can't relate to itself
            db.ArticleRelations.Add(new ArticleRelation
            {
                ArticleId = articleId,
                RelatedArticleId = relatedId,
                SortOrder = order++
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<Article>> SearchArticlesAsync(string query, int? categoryId = null, List<int>? tagIds = null, bool includeDrafts = false)
    {
        using var db = _dbFactory.CreateDbContext();
        var q = (query ?? "").Trim().ToLower();

        var baseQuery = db.Articles
            .Include(a => a.Category)
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .AsQueryable();

        if (!includeDrafts)
            baseQuery = baseQuery.Where(a => a.IsPublished && (a.ScheduledPublishAt == null || a.ScheduledPublishAt <= DateTime.UtcNow));

        if (categoryId.HasValue)
            baseQuery = baseQuery.Where(a => a.CategoryId == categoryId.Value);

        if (tagIds != null && tagIds.Any())
            baseQuery = baseQuery.Where(a => a.ArticleTags.Any(at => tagIds.Contains(at.TagId)));

        if (string.IsNullOrEmpty(q))
        {
            return await baseQuery.OrderBy(a => a.CategoryId).ThenBy(a => a.SortOrder).ToListAsync();
        }

        return await baseQuery
            .Where(a => a.Title.ToLower().Contains(q)
                     || a.Summary.ToLower().Contains(q)
                     || a.Tags!.ToLower().Contains(q)
                     || (a.Category != null && a.Category.Name.ToLower().Contains(q))
                     || a.ArticleTags.Any(at => at.Tag != null && at.Tag.Name.ToLower().Contains(q)))
            .ToListAsync();
    }

    public async Task IncrementViewCountAsync(int articleId)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.FindAsync(articleId);
        if (article != null)
        {
            article.ViewCount++;
            await db.SaveChangesAsync();
        }
    }

    // ---------- Article writes ----------

    public async Task<Article> CreateArticleAsync(Article article, string createdBy)
    {
        using var db = _dbFactory.CreateDbContext();
        article.Id = 0;
        article.CreatedAt = DateTime.UtcNow;
        article.UpdatedAt = DateTime.UtcNow;
        
        if (article.IsPublished && article.PublishedAt == null)
            article.PublishedAt = DateTime.UtcNow;
            
        if (string.IsNullOrWhiteSpace(article.Slug))
            article.Slug = GenerateSlug(article.Title);

        db.Articles.Add(article);
        await db.SaveChangesAsync();

        await LogAuditAsync(db, createdBy, AuditAction.Created, AuditEntityType.Article, article.Id, article.Title);
        
        return article;
    }

    /// <summary>Saves an article's editable fields and (optionally) replaces its steps.
    ///
    /// <paramref name="steps"/> of <c>null</c> means "leave the steps exactly as they are".
    /// Use that from any caller that only changes article-level fields and hasn't loaded
    /// the steps — passing an empty list instead deletes every step the article has,
    /// which is how steps used to silently vanish when publishing from the articles list.
    ///
    /// <paramref name="expectedUpdatedAt"/> is an optional stale-write guard: pass the
    /// UpdatedAt value this session last saw, and the write is skipped if someone (or
    /// another editor session/tab) has saved the article since. Returns the article's new
    /// UpdatedAt on success, or <c>null</c> if nothing was written.</summary>
    public async Task<DateTime?> UpdateArticleAsync(Article article, List<ArticleStep>? steps, string updatedBy, string? changeNotes = null, DateTime? expectedUpdatedAt = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.Articles
            .Include(a => a.Steps)
            .Include(a => a.ArticleTags)
            .FirstOrDefaultAsync(a => a.Id == article.Id);
        
        if (existing is null) return null;

        if (expectedUpdatedAt is not null
            && Math.Abs((existing.UpdatedAt - expectedUpdatedAt.Value).TotalSeconds) > 1)
        {
            // The row moved on since this caller read it. Refuse rather than
            // overwrite work that isn't in this caller's copy of the article.
            return null;
        }

        var oldIsPublished = existing.IsPublished;

        existing.Title = article.Title;
        existing.Summary = article.Summary;
        existing.CategoryId = article.CategoryId;
        existing.VideoUrl = article.VideoUrl;
        var savedAt = DateTime.UtcNow;
        existing.UpdatedAt = savedAt;
        existing.Tags = article.Tags;
        existing.IsPublished = article.IsPublished;
        existing.IsFeatured = article.IsFeatured;
        existing.ScheduledPublishAt = article.ScheduledPublishAt;
        existing.MetaTitle = article.MetaTitle;
        existing.MetaDescription = article.MetaDescription;
        existing.ParentArticleId = article.ParentArticleId;

        if (article.IsPublished && !oldIsPublished && existing.PublishedAt == null)
            existing.PublishedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(existing.Slug))
            existing.Slug = GenerateSlug(article.Title);

        // Update steps — but only if the caller actually supplied them.
        List<ArticleStep> stepsForVersion;
        if (steps is null)
        {
            stepsForVersion = existing.Steps.OrderBy(s => s.SortOrder).ToList();
        }
        else
        {
            var replacement = new List<ArticleStep>();
            var order = 1;
            foreach (var step in steps)
            {
                if (IsBlankStep(step))
                    continue;

                replacement.Add(new ArticleStep
                {
                    Title = step.Title,
                    Description = step.Description,
                    ImageUrl = step.ImageUrl,
                    VideoUrl = step.VideoUrl,
                    ImageAlt = step.ImageAlt,
                    EstimatedMinutes = step.EstimatedMinutes,
                    IsOptional = step.IsOptional,
                    SortOrder = order++
                });
            }

            // .ToList() matters: marking these Deleted makes EF detach them from
            // existing.Steps, i.e. it mutates the very collection RemoveRange would
            // otherwise still be enumerating.
            db.ArticleSteps.RemoveRange(existing.Steps.ToList());
            foreach (var step in replacement)
            {
                existing.Steps.Add(step);
            }

            stepsForVersion = replacement;
        }

        // Create version snapshot. Built from the step list we just decided on —
        // reading existing.Steps here would mix the outgoing (deleted) steps in with
        // the incoming ones, since none of it has been flushed to the database yet.
        await CreateVersionSnapshotAsync(db, existing, stepsForVersion, updatedBy, changeNotes ?? "Updated article");

        await db.SaveChangesAsync();

        await LogAuditAsync(db, updatedBy, AuditAction.Updated, AuditEntityType.Article, article.Id, article.Title);

        return savedAt;
    }

    /// <summary>A step row that carries nothing at all — no text, no photo, no video —
    /// is just an empty slot in the editor and isn't worth persisting. Note that a step
    /// with only a photo (a screenshot with no caption yet) is NOT blank: those used to
    /// be thrown away on save.</summary>
    private static bool IsBlankStep(ArticleStep step) =>
        string.IsNullOrWhiteSpace(step.Title)
        && string.IsNullOrWhiteSpace(step.Description)
        && string.IsNullOrWhiteSpace(step.ImageUrl)
        && string.IsNullOrWhiteSpace(step.VideoUrl);

    /// <summary>Flips only the published flag, without touching steps/tags or writing a
    /// version snapshot. Used as a lightweight safety net (e.g. when an editor session
    /// is abandoned — browser closed, tab navigated away — without an explicit Save),
    /// so it never clobbers content someone else may be mid-typing elsewhere.</summary>
    public async Task SetPublishedStateAsync(int id, bool isPublished, string updatedBy, string reason)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (article is null || article.IsPublished == isPublished) return;

        article.IsPublished = isPublished;
        if (isPublished && article.PublishedAt is null)
            article.PublishedAt = DateTime.UtcNow;
        article.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await LogAuditAsync(db, updatedBy, isPublished ? AuditAction.Published : AuditAction.Unpublished,
            AuditEntityType.Article, id, article.Title, reason);
    }

    /// <summary>Restores an article's editable fields, steps and tags back to a prior
    /// in-memory snapshot. Used by the editor's Cancel button to undo whatever the
    /// background autosave already wrote to the database during this session — unlike
    /// UpdateArticleAsync, this does not create a new version-history entry, since
    /// nothing new was actually authored.</summary>
    public async Task RevertArticleAsync(Article snapshot, List<ArticleStep> steps, List<int> tagIds, string revertedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.Articles
            .Include(a => a.Steps)
            .Include(a => a.ArticleTags)
            .FirstOrDefaultAsync(a => a.Id == snapshot.Id);

        if (existing is null) return;

        existing.Title = snapshot.Title;
        existing.Slug = snapshot.Slug;
        existing.Summary = snapshot.Summary;
        existing.VideoUrl = snapshot.VideoUrl;
        existing.CategoryId = snapshot.CategoryId;
        existing.MetaTitle = snapshot.MetaTitle;
        existing.MetaDescription = snapshot.MetaDescription;
        existing.IsPublished = snapshot.IsPublished;
        existing.IsFeatured = snapshot.IsFeatured;
        existing.ScheduledPublishAt = snapshot.ScheduledPublishAt;
        existing.UpdatedAt = DateTime.UtcNow;

        db.ArticleSteps.RemoveRange(existing.Steps.ToList());
        var order = 1;
        foreach (var step in steps)
        {
            existing.Steps.Add(new ArticleStep
            {
                Title = step.Title,
                Description = step.Description,
                ImageUrl = step.ImageUrl,
                VideoUrl = step.VideoUrl,
                ImageAlt = step.ImageAlt,
                EstimatedMinutes = step.EstimatedMinutes,
                IsOptional = step.IsOptional,
                SortOrder = order++
            });
        }

        existing.ArticleTags.Clear();
        foreach (var tagId in tagIds)
        {
            existing.ArticleTags.Add(new ArticleTag { TagId = tagId });
        }

        await db.SaveChangesAsync();
        await LogAuditAsync(db, revertedBy, AuditAction.Restored, AuditEntityType.Article, existing.Id,
            existing.Title, "Reverted — edit session was cancelled");
    }

    public async Task DeleteArticleAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.FindAsync(id);
        if (article != null)
        {
            var title = article.Title;
            article.IsDeleted = true;
            article.DeletedAt = DateTime.UtcNow;
            article.DeletedBy = deletedBy;
            await db.SaveChangesAsync();
            await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Article, id, title);
        }
    }

    public async Task<List<Article>> GetDeletedArticlesAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Articles.IgnoreQueryFilters()
            .Include(a => a.Category)
            .Where(a => a.IsDeleted)
            .OrderByDescending(a => a.DeletedAt)
            .ToListAsync();
    }

    public async Task<bool> RestoreArticleAsync(int id, string restoredBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted);
        if (article is null) return false;

        var slugTaken = await db.Articles.IgnoreQueryFilters()
            .AnyAsync(a => a.Id != id && a.Slug == article.Slug && !a.IsDeleted);
        if (slugTaken) return false;

        // The article's category may itself still be in the Recycle Bin (it was
        // soft-deleted along with it) — restoring the article alone would otherwise
        // leave it invisible everywhere despite IsDeleted = false.
        var categoryDeleted = await db.Categories.IgnoreQueryFilters()
            .AnyAsync(c => c.Id == article.CategoryId && c.IsDeleted);
        if (categoryDeleted) return false;

        article.IsDeleted = false;
        article.DeletedAt = null;
        article.DeletedBy = null;
        await db.SaveChangesAsync();
        await LogAuditAsync(db, restoredBy, AuditAction.Restored, AuditEntityType.Article, id, article.Title);
        return true;
    }

    public async Task PermanentlyDeleteArticleAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted);
        if (article != null)
        {
            var title = article.Title;
            db.Articles.Remove(article);
            await db.SaveChangesAsync();
            await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Article, id, title + " (permanently)");
        }
    }

    public async Task<Article?> DuplicateArticleAsync(int id, string createdBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var source = await db.Articles
            .Include(a => a.Steps)
            .Include(a => a.ArticleTags)
            .FirstOrDefaultAsync(a => a.Id == id);
        
        if (source == null) return null;

        var duplicate = new Article
        {
            CategoryId = source.CategoryId,
            Title = source.Title + " (Copy)",
            Summary = source.Summary,
            VideoUrl = source.VideoUrl,
            SortOrder = source.SortOrder + 1,
            IsPublished = false,
            IsFeatured = false,
            ViewCount = 0,
            Tags = source.Tags,
            MetaTitle = source.MetaTitle,
            MetaDescription = source.MetaDescription
        };

        duplicate.Slug = GenerateSlug(duplicate.Title);

        db.Articles.Add(duplicate);
        await db.SaveChangesAsync();

        foreach (var step in source.Steps.OrderBy(s => s.SortOrder))
        {
            duplicate.Steps.Add(new ArticleStep
            {
                Title = step.Title,
                Description = step.Description,
                ImageUrl = step.ImageUrl,
                VideoUrl = step.VideoUrl,
                ImageAlt = step.ImageAlt,
                EstimatedMinutes = step.EstimatedMinutes,
                IsOptional = step.IsOptional,
                SortOrder = step.SortOrder
            });
        }

        foreach (var at in source.ArticleTags)
        {
            duplicate.ArticleTags.Add(new ArticleTag { TagId = at.TagId });
        }

        await db.SaveChangesAsync();

        await LogAuditAsync(db, createdBy, AuditAction.Created, AuditEntityType.Article, duplicate.Id, duplicate.Title + " (duplicated from " + source.Title + ")");

        return duplicate;
    }

    // ---------- Category writes ----------

    /// <summary>Creates a category. Returns null with a caller-facing <paramref name="error"/>
    /// if the (trimmed, case-insensitive) name or the resulting slug is already taken —
    /// including by something currently sitting in the Recycle Bin, since its slug is
    /// still reserved until it's restored or permanently deleted.</summary>
    public async Task<(Category? category, string? error)> CreateCategoryAsync(string name, string? icon = null, string? description = null, string? color = null, string? createdBy = null, string? customSlug = null, int? parentCategoryId = null)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (null, "Name is required.");

        var slug = GenerateSlug(string.IsNullOrWhiteSpace(customSlug) ? name : customSlug);
        if (string.IsNullOrWhiteSpace(slug))
            return (null, "That name doesn't produce a usable slug — try adding a letter or number.");

        using var db = _dbFactory.CreateDbContext();

        if (parentCategoryId is not null)
        {
            var parent = await db.Categories.FindAsync(parentCategoryId.Value);
            if (parent is null)
                return (null, "Parent category not found.");
            if (parent.ParentCategoryId is not null)
                return (null, "That category is itself a subcategory — subcategories can only be one level deep.");
        }

        var duplicate = await FindCategoryDuplicateAsync(db, name, slug, excludeId: null);
        if (duplicate != null)
            return (null, DuplicateMessage("category", duplicate.Name, duplicate.IsDeleted));

        var maxOrder = await db.Categories.Where(c => c.ParentCategoryId == parentCategoryId).MaxAsync(c => (int?)c.SortOrder) ?? 0;
        var category = new Category 
        { 
            Name = name, 
            Slug = slug,
            Icon = string.IsNullOrWhiteSpace(icon) ? "📁" : icon,
            Description = description,
            Color = color,
            SortOrder = maxOrder + 1,
            ParentCategoryId = parentCategoryId
        };
        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Defense in depth: a concurrent request created the same slug between our
            // check above and this save. Either way, don't let a raw DB exception reach
            // the caller and take down the Blazor circuit — surface it as a normal error.
            return (null, "A category with that name or slug already exists.");
        }
        
        if (!string.IsNullOrEmpty(createdBy))
            await LogAuditAsync(db, createdBy, AuditAction.Created, AuditEntityType.Category, category.Id, category.Name);
            
        return (category, null);
    }

    public async Task<string?> UpdateCategoryAsync(Category category, string updatedBy)
    {
        var name = (category.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";

        using var db = _dbFactory.CreateDbContext();
        var existing = await db.Categories.FindAsync(category.Id);
        if (existing is null) return "Category not found.";

        if (category.ParentCategoryId is not null)
        {
            if (category.ParentCategoryId == category.Id)
                return "A category can't be its own parent.";

            var parent = await db.Categories.FindAsync(category.ParentCategoryId.Value);
            if (parent is null)
                return "Parent category not found.";
            if (parent.ParentCategoryId is not null)
                return "That category is itself a subcategory — subcategories can only be one level deep.";

            var hasChildren = await db.Categories.AnyAsync(c => c.ParentCategoryId == category.Id);
            if (hasChildren)
                return "This category has its own subcategories, so it can't be turned into one — move or remove them first.";
        }

        var slug = string.IsNullOrWhiteSpace(category.Slug) ? GenerateSlug(name) : GenerateSlug(category.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return "That name doesn't produce a usable slug — try adding a letter or number.";

        var duplicate = await FindCategoryDuplicateAsync(db, name, slug, excludeId: category.Id);
        if (duplicate != null)
            return DuplicateMessage("category", duplicate.Name, duplicate.IsDeleted);

        existing.Name = name;
        existing.Slug = slug;
        existing.Icon = category.Icon;
        existing.Description = category.Description;
        existing.Color = category.Color;
        existing.SortOrder = category.SortOrder;
        existing.IsVisible = category.IsVisible;
        existing.ParentCategoryId = category.ParentCategoryId;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return "A category with that name or slug already exists.";
        }
        
        await LogAuditAsync(db, updatedBy, AuditAction.Updated, AuditEntityType.Category, category.Id, existing.Name);
        return null;
    }

    /// <summary>Moves a category to the Recycle Bin instead of deleting it outright.
    /// 
    /// Subcategory deletion: articles are reassigned to the parent category so they
    /// are NOT lost — only the subcategory itself moves to the Recycle Bin.
    ///
    /// Top-level category deletion: subcategories and their articles move to the Recycle
    /// Bin along with the parent (they can be restored independently from RecycleBin).</summary>
    public async Task DeleteCategoryAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var category = await db.Categories
            .Include(c => c.Articles)
            .Include(c => c.SubCategories).ThenInclude(s => s.Articles)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return;

        var now = DateTime.UtcNow;

        if (category.ParentCategoryId is not null)
        {
            // Deleting a subcategory: reassign its articles to the parent category
            // so articles are preserved — only the subcategory shell is binned.
            foreach (var article in category.Articles.Where(a => !a.IsDeleted))
            {
                article.CategoryId = category.ParentCategoryId.Value;
            }

            category.IsDeleted = true;
            category.DeletedAt = now;
            category.DeletedBy = deletedBy;
        }
        else
        {
            // Deleting a top-level category: soft-delete it and all its subcategories.
            // Articles inside each are also soft-deleted (restorable from Recycle Bin).
            void SoftDelete(Category c)
            {
                c.IsDeleted = true;
                c.DeletedAt = now;
                c.DeletedBy = deletedBy;
                foreach (var article in c.Articles.Where(a => !a.IsDeleted))
                {
                    article.IsDeleted = true;
                    article.DeletedAt = now;
                    article.DeletedBy = deletedBy;
                }
            }

            SoftDelete(category);
            foreach (var sub in category.SubCategories.Where(s => !s.IsDeleted))
                SoftDelete(sub);
        }

        await db.SaveChangesAsync();
        await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Category, id, category.Name);
    }

    public async Task<List<Category>> GetDeletedCategoriesAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Categories.IgnoreQueryFilters()
            .Where(c => c.IsDeleted)
            .OrderByDescending(c => c.DeletedAt)
            .ToListAsync();
    }

    public async Task<bool> RestoreCategoryAsync(int id, string restoredBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var category = await db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);
        if (category is null) return false;

        // A category or article created after this one was deleted might already
        // occupy its slug — restoring would violate the unique index.
        var slugTaken = await db.Categories.IgnoreQueryFilters()
            .AnyAsync(c => c.Id != id && c.Slug == category.Slug && !c.IsDeleted);
        if (slugTaken) return false;

        category.IsDeleted = false;
        category.DeletedAt = null;
        category.DeletedBy = null;
        await db.SaveChangesAsync();
        await LogAuditAsync(db, restoredBy, AuditAction.Restored, AuditEntityType.Category, id, category.Name);
        return true;
    }

    public async Task PermanentlyDeleteCategoryAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var category = await db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);
        if (category != null)
        {
            var name = category.Name;
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Category, id, name + " (permanently)");
        }
    }

    private static async Task<Category?> FindCategoryDuplicateAsync(AppDbContext db, string name, string slug, int? excludeId)
    {
        return await db.Categories.IgnoreQueryFilters()
            .Where(c => excludeId == null || c.Id != excludeId)
            .FirstOrDefaultAsync(c => c.Slug == slug || c.Name.ToLower() == name.ToLower());
    }

    private static string DuplicateMessage(string kind, string existingName, bool isDeleted) => isDeleted
        ? $"A {kind} named \"{existingName}\" already exists in the Recycle Bin. Restore or permanently delete it first."
        : $"A {kind} named \"{existingName}\" already exists.";

    // ---------- Tags ----------

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        // Include ArticleTags so the "in use, can't delete" guard on the Tags admin
        // page actually reflects real usage instead of always reading as unused.
        return await db.Tags.Include(t => t.ArticleTags).OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<Tag?> GetTagAsync(int id)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Tags.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(Tag? tag, string? error)> CreateTagAsync(string name, string? color = null, string? createdBy = null)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (null, "Name is required.");

        var slug = GenerateSlug(name);
        if (string.IsNullOrWhiteSpace(slug))
            return (null, "That name doesn't produce a usable slug — try adding a letter or number.");

        using var db = _dbFactory.CreateDbContext();

        var duplicate = await FindTagDuplicateAsync(db, name, slug, excludeId: null);
        if (duplicate != null)
            return (null, DuplicateMessage("tag", duplicate.Name, duplicate.IsDeleted));

        var tag = new Tag 
        { 
            Name = name, 
            Slug = slug,
            Color = color ?? "#1F7A4D"
        };
        db.Tags.Add(tag);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (null, "A tag with that name already exists.");
        }
        
        if (!string.IsNullOrEmpty(createdBy))
            await LogAuditAsync(db, createdBy, AuditAction.Created, AuditEntityType.Tag, tag.Id, tag.Name);
            
        return (tag, null);
    }

    public async Task<string?> UpdateTagAsync(Tag tag, string updatedBy)
    {
        var name = (tag.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";

        using var db = _dbFactory.CreateDbContext();
        var existing = await db.Tags.FindAsync(tag.Id);
        if (existing is null) return "Tag not found.";

        var slug = string.IsNullOrWhiteSpace(tag.Slug) ? GenerateSlug(name) : GenerateSlug(tag.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return "That name doesn't produce a usable slug — try adding a letter or number.";

        var duplicate = await FindTagDuplicateAsync(db, name, slug, excludeId: tag.Id);
        if (duplicate != null)
            return DuplicateMessage("tag", duplicate.Name, duplicate.IsDeleted);

        existing.Name = name;
        existing.Slug = slug;
        existing.Color = tag.Color;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return "A tag with that name already exists.";
        }

        await LogAuditAsync(db, updatedBy, AuditAction.Updated, AuditEntityType.Tag, tag.Id, existing.Name);
        return null;
    }

    public async Task DeleteTagAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var tag = await db.Tags.FindAsync(id);
        if (tag != null)
        {
            var name = tag.Name;
            tag.IsDeleted = true;
            tag.DeletedAt = DateTime.UtcNow;
            tag.DeletedBy = deletedBy;
            await db.SaveChangesAsync();
            await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Tag, id, name);
        }
    }

    public async Task<List<Tag>> GetDeletedTagsAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.Tags.IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync();
    }

    public async Task<bool> RestoreTagAsync(int id, string restoredBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var tag = await db.Tags.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted);
        if (tag is null) return false;

        var slugTaken = await db.Tags.IgnoreQueryFilters()
            .AnyAsync(t => t.Id != id && t.Slug == tag.Slug && !t.IsDeleted);
        if (slugTaken) return false;

        tag.IsDeleted = false;
        tag.DeletedAt = null;
        tag.DeletedBy = null;
        await db.SaveChangesAsync();
        await LogAuditAsync(db, restoredBy, AuditAction.Restored, AuditEntityType.Tag, id, tag.Name);
        return true;
    }

    public async Task PermanentlyDeleteTagAsync(int id, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var tag = await db.Tags.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted);
        if (tag != null)
        {
            var name = tag.Name;
            db.Tags.Remove(tag);
            await db.SaveChangesAsync();
            await LogAuditAsync(db, deletedBy, AuditAction.Deleted, AuditEntityType.Tag, id, name + " (permanently)");
        }
    }

    private static async Task<Tag?> FindTagDuplicateAsync(AppDbContext db, string name, string slug, int? excludeId)
    {
        return await db.Tags.IgnoreQueryFilters()
            .Where(t => excludeId == null || t.Id != excludeId)
            .FirstOrDefaultAsync(t => t.Slug == slug || t.Name.ToLower() == name.ToLower());
    }

    public async Task SetArticleTagsAsync(int articleId, List<int> tagIds, string updatedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var article = await db.Articles.Include(a => a.ArticleTags).FirstOrDefaultAsync(a => a.Id == articleId);
        if (article == null) return;

        article.ArticleTags.Clear();
        foreach (var tagId in tagIds)
        {
            article.ArticleTags.Add(new ArticleTag { TagId = tagId });
        }
        await db.SaveChangesAsync();
        await LogAuditAsync(db, updatedBy, AuditAction.Updated, AuditEntityType.Article, articleId, "Tags updated");
    }

    // ---------- Article Versions ----------

    public async Task<List<ArticleVersion>> GetArticleVersionsAsync(int articleId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.ArticleVersions
            .Include(v => v.StepVersions)
            .Where(v => v.ArticleId == articleId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    public async Task<ArticleVersion?> GetArticleVersionAsync(int versionId)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.ArticleVersions
            .Include(v => v.StepVersions)
            .FirstOrDefaultAsync(v => v.Id == versionId);
    }

    public async Task RestoreArticleVersionAsync(int articleId, int versionId, string restoredBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var version = await db.ArticleVersions
            .Include(v => v.StepVersions)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.ArticleId == articleId);
        
        if (version == null) return;

        var article = await db.Articles
            .Include(a => a.Steps)
            .FirstOrDefaultAsync(a => a.Id == articleId);
        
        if (article == null) return;

        article.Title = version.Title;
        article.Summary = version.Summary;
        article.VideoUrl = version.VideoUrl;
        article.UpdatedAt = DateTime.UtcNow;

        db.ArticleSteps.RemoveRange(article.Steps.ToList());
        foreach (var sv in version.StepVersions.OrderBy(sv => sv.SortOrder))
        {
            article.Steps.Add(new ArticleStep
            {
                Title = sv.Title,
                Description = sv.Description,
                ImageUrl = sv.ImageUrl,
                VideoUrl = sv.VideoUrl,
                SortOrder = sv.SortOrder
            });
        }

        await db.SaveChangesAsync();
        await LogAuditAsync(db, restoredBy, AuditAction.Restored, AuditEntityType.Article, articleId, $"Restored to version {version.VersionNumber}");
    }

    // ---------- Feedback ----------

    // The feedback widget's inputs are plain @bind fields with no DataAnnotationsValidator
    // (unlike the guest-entry form), so nothing before this point limits how much text can
    // be submitted. Under real traffic that's an easy way to bloat the database with
    // oversized rows — accidentally (a pasted document) or deliberately (a scripted flood
    // of huge comments). Truncating here, in the one place every caller funnels through,
    // guarantees the cap holds regardless of what UI ends up calling this.
    private const int MaxFeedbackCompanyLength = 200;
    private const int MaxFeedbackNameLength = 200;
    private const int MaxFeedbackCommentLength = 2000;

    private static string? TruncateOrNull(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : (value.Length > maxLength ? value[..maxLength] : value);

    public async Task AddFeedbackAsync(int articleId, FeedbackRating rating, string? comment = null, string? userAgent = null, string? ipAddress = null, string? company = null, string? name = null)
    {
        using var db = _dbFactory.CreateDbContext();
        db.ArticleFeedbacks.Add(new ArticleFeedback 
        { 
            ArticleId = articleId, 
            Rating = rating,
            Comment = TruncateOrNull(comment, MaxFeedbackCommentLength),
            UserAgent = userAgent,
            IpAddress = ipAddress,
            Company = TruncateOrNull(company, MaxFeedbackCompanyLength) ?? "",
            Name = TruncateOrNull(name, MaxFeedbackNameLength)
        });
        await db.SaveChangesAsync();
    }

    public async Task<(int sad, int ok, int verySatisfied)> GetFeedbackSummaryAsync(int articleId)
    {
        using var db = _dbFactory.CreateDbContext();
        var items = await db.ArticleFeedbacks.Where(f => f.ArticleId == articleId).ToListAsync();
        return (
            items.Count(f => f.Rating == FeedbackRating.Sad),
            items.Count(f => f.Rating == FeedbackRating.Ok),
            items.Count(f => f.Rating == FeedbackRating.VerySatisfied)
        );
    }

    public async Task<List<ArticleFeedback>> GetArticleFeedbackAsync(int articleId, bool includePrivate = false)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.ArticleFeedbacks.Where(f => f.ArticleId == articleId);
        if (!includePrivate)
            query = query.Where(f => f.IsPublic);
        return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
    }

    /// <summary>"Is this helpful?" stats rolled up per category/module, for the admin dashboard.</summary>
    public async Task<List<CategoryFeedbackSummary>> GetFeedbackSummaryByCategoryAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        var categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        var feedback = await db.ArticleFeedbacks.Include(f => f.Article).ToListAsync();

        return categories.Select(c =>
        {
            var items = feedback.Where(f => f.Article!.CategoryId == c.Id).ToList();
            var sad = items.Count(f => f.Rating == FeedbackRating.Sad);
            var ok = items.Count(f => f.Rating == FeedbackRating.Ok);
            var happy = items.Count(f => f.Rating == FeedbackRating.VerySatisfied);
            var total = items.Count;
            return new CategoryFeedbackSummary
            {
                CategoryName = c.Name,
                Sad = sad,
                Ok = ok,
                VerySatisfied = happy,
                Total = total,
                SatisfactionPercent = total == 0 ? 0 : Math.Round((sad * 0 + ok * 0.5 + happy * 1.0) / total * 100, 0)
            };
        }).ToList();
    }

    // ---------- Dashboard Stats ----------

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        
        var totalArticles = await db.Articles.CountAsync();
        var publishedArticles = await db.Articles.CountAsync(a => a.IsPublished);
        var draftArticles = totalArticles - publishedArticles;
        var totalCategories = await db.Categories.CountAsync();
        var totalFeedback = await db.ArticleFeedbacks.CountAsync();
        var totalViews = await db.Articles.SumAsync(a => (long)a.ViewCount);
        
        var recentFeedback = await db.ArticleFeedbacks
            .Include(f => f.Article)
            .OrderByDescending(f => f.CreatedAt)
            .Take(10)
            .ToListAsync();

        var topArticles = await db.Articles
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.ViewCount)
            .Take(5)
            .Select(a => new TopArticle { Id = a.Id, Title = a.Title, ViewCount = a.ViewCount, CategoryName = a.Category!.Name })
            .ToListAsync();

        return new DashboardStats
        {
            TotalArticles = totalArticles,
            PublishedArticles = publishedArticles,
            DraftArticles = draftArticles,
            TotalCategories = totalCategories,
            TotalFeedback = totalFeedback,
            TotalViews = totalViews,
            RecentFeedback = recentFeedback,
            TopArticles = topArticles
        };
    }

    // ---------- Audit Log ----------

    private async Task LogAuditAsync(AppDbContext db, string username, AuditAction action, AuditEntityType entityType, int entityId, string entityName, string? changesJson = null)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            Action = action.ToString(),
            EntityType = entityType.ToString(),
            EntityId = entityId,
            EntityName = entityName,
            Username = username,
            ChangesJson = changesJson,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task CreateVersionSnapshotAsync(AppDbContext db, Article article, IEnumerable<ArticleStep> steps, string createdBy, string changeNotes)
    {
        var versionNumber = await db.ArticleVersions
            .Where(v => v.ArticleId == article.Id)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        var version = new ArticleVersion
        {
            ArticleId = article.Id,
            Title = article.Title,
            Summary = article.Summary,
            VideoUrl = article.VideoUrl,
            VersionNumber = versionNumber + 1,
            ChangeNotes = changeNotes,
            CreatedBy = createdBy
        };

        foreach (var step in steps.OrderBy(s => s.SortOrder))
        {
            version.StepVersions.Add(new ArticleStepVersion
            {
                SortOrder = step.SortOrder,
                Title = step.Title,
                Description = step.Description,
                ImageUrl = step.ImageUrl,
                VideoUrl = step.VideoUrl
            });
        }

        db.ArticleVersions.Add(version);
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("&", "and")
            .Replace("/", "-");
        
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }
}

public class CategoryFeedbackSummary
{
    public string CategoryName { get; set; } = "";
    public int Sad { get; set; }
    public int Ok { get; set; }
    public int VerySatisfied { get; set; }
    public int Total { get; set; }
    public double SatisfactionPercent { get; set; }
}

public class DashboardStats
{
    public int TotalArticles { get; set; }
    public int PublishedArticles { get; set; }
    public int DraftArticles { get; set; }
    public int TotalCategories { get; set; }
    public int TotalFeedback { get; set; }
    public long TotalViews { get; set; }
    public List<ArticleFeedback> RecentFeedback { get; set; } = new();
    public List<TopArticle> TopArticles { get; set; } = new();
}

public class TopArticle
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int ViewCount { get; set; }
    public string CategoryName { get; set; } = "";
}
