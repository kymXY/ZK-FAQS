using FaqCms.Models;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleStep> ArticleSteps => Set<ArticleStep>();
    public DbSet<ArticleFeedback> ArticleFeedbacks => Set<ArticleFeedback>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<ArticleStepVersion> ArticleStepVersions => Set<ArticleStepVersion>();
    public DbSet<ArticleRelation> ArticleRelations => Set<ArticleRelation>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<HelpdeskSettings> HelpdeskSettings => Set<HelpdeskSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        // Recycle Bin: soft-deleted rows (IsDeleted = true) are hidden from every normal
        // query automatically. Recycle Bin screens use .IgnoreQueryFilters() to see them.
        modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.Articles)
            .WithOne(a => a.Category!)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.SubCategories)
            .WithOne(c => c.ParentCategory!)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        modelBuilder.Entity<Article>().HasQueryFilter(a => !a.IsDeleted);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.Steps)
            .WithOne(s => s.Article!)
            .HasForeignKey(s => s.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.Feedback)
            .WithOne(f => f.Article!)
            .HasForeignKey(f => f.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.Versions)
            .WithOne(v => v.Article!)
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.RelatedArticles)
            .WithOne(a => a.ParentArticle!)
            .HasForeignKey(a => a.ParentArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.ArticleTags)
            .WithOne(at => at.Article!)
            .HasForeignKey(at => at.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Manual related-article picks. Two FKs to Articles from the same table, so the
        // "owning" side (ArticleId — the article whose editor this row belongs to)
        // cascades on delete; the "target" side (RelatedArticleId) does not, since SQL
        // Server refuses more than one cascade path into the same table. A hard delete
        // of an article this way just leaves a dangling relation row behind, which is
        // harmless — GetRelatedArticlesAsync only ever returns published, non-deleted
        // targets, so it's filtered out at read time either way.
        modelBuilder.Entity<ArticleRelation>()
            .HasOne(r => r.Article)
            .WithMany(a => a.ManualRelatedArticles)
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticleRelation>()
            .HasOne(r => r.RelatedArticle)
            .WithMany()
            .HasForeignKey(r => r.RelatedArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ArticleRelation>()
            .HasIndex(r => new { r.ArticleId, r.RelatedArticleId })
            .IsUnique();

        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<Tag>().HasQueryFilter(t => !t.IsDeleted);

        modelBuilder.Entity<Tag>()
            .HasMany(t => t.ArticleTags)
            .WithOne(at => at.Tag!)
            .HasForeignKey(at => at.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArticleTag>()
            .HasKey(at => new { at.ArticleId, at.TagId });

        modelBuilder.Entity<ArticleVersion>()
            .HasMany(v => v.StepVersions)
            .WithOne(sv => sv.ArticleVersion!)
            .HasForeignKey(sv => sv.ArticleVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuditLogEntry>()
            .HasIndex(a => new { a.EntityType, a.EntityId });

        modelBuilder.Entity<AuditLogEntry>()
            .HasOne(a => a.AdminUser)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.AdminUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.Role, rp.PermissionKey })
            .IsUnique();

        SeedData.Seed(modelBuilder);
    }
}
