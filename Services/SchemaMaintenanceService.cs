using FaqCms.Data;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Services;

/// <summary>
/// One-off, additive schema tweaks applied on startup instead of an EF Core migration —
/// same pattern as PermissionService/SettingsService's EnsureSchemaAndDefaultsAsync,
/// used here because it's safe to run against a database that predates these columns
/// (a fresh DB created by the existing migrations won't have them either) without
/// needing the EF Core CLI tools to generate a formal migration.
/// Safe to call on every startup: every statement below is idempotent.
/// </summary>
public class SchemaMaintenanceService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SchemaMaintenanceService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>Adds the Recycle Bin (soft-delete) columns to Categories, Tags and
    /// Articles if they're not already there.</summary>
    public async Task EnsureRecycleBinSchemaAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        // `table` is only ever one of these three hardcoded literals — never anything
        // derived from user input — so this is not actually exploitable. It's flagged
        // (EF1002) because `ALTER TABLE [{table}]` uses the value as a bare SQL
        // identifier, which T-SQL DDL simply has no way to parameterize; ExecuteSqlRaw
        // is the correct tool here, not ExecuteSqlInterpolated. If this array is ever
        // changed to pull table names from anywhere other than a hardcoded literal,
        // that change needs its own injection review — don't just carry this
        // suppression forward.
        foreach (var table in new[] { "Categories", "Tags", "Articles" })
        {
#pragma warning disable EF1002 // table name is a hardcoded literal — see comment above
            await db.Database.ExecuteSqlRawAsync($@"
                IF COL_LENGTH('[{table}]', 'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [{table}] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_{table}_IsDeleted] DEFAULT 0;
                END
                IF COL_LENGTH('[{table}]', 'DeletedAt') IS NULL
                BEGIN
                    ALTER TABLE [{table}] ADD [DeletedAt] datetime2 NULL;
                END
                IF COL_LENGTH('[{table}]', 'DeletedBy') IS NULL
                BEGIN
                    ALTER TABLE [{table}] ADD [DeletedBy] nvarchar(max) NULL;
                END");
#pragma warning restore EF1002
        }
    }

    /// <summary>Adds the ParentCategoryId column (subcategory support) to Categories if
    /// it's not already there, along with its self-referencing foreign key and an index —
    /// same additive, idempotent pattern as EnsureRecycleBinSchemaAsync above.</summary>
    public async Task EnsureSubCategorySchemaAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(@"
            IF COL_LENGTH('[Categories]', 'ParentCategoryId') IS NULL
            BEGIN
                ALTER TABLE [Categories] ADD [ParentCategoryId] int NULL;
                ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Categories_ParentCategoryId]
                    FOREIGN KEY ([ParentCategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION;
                CREATE INDEX [IX_Categories_ParentCategoryId] ON [Categories] ([ParentCategoryId]);
            END");
    }

    /// <summary>Adds manual related-article support: the RelatedArticlesMode column on
    /// Articles, and the ArticleRelations join table backing the editor's "choose them
    /// myself" picker. Additive and idempotent, same pattern as the methods above.</summary>
    public async Task EnsureRelatedArticlesSchemaAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(@"
            IF COL_LENGTH('[Articles]', 'RelatedArticlesMode') IS NULL
            BEGIN
                ALTER TABLE [Articles] ADD [RelatedArticlesMode] nvarchar(20) NOT NULL
                    CONSTRAINT [DF_Articles_RelatedArticlesMode] DEFAULT 'auto';
            END");

        await db.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[ArticleRelations]', 'U') IS NULL
            BEGIN
                CREATE TABLE [ArticleRelations] (
                    [Id] int NOT NULL IDENTITY,
                    [ArticleId] int NOT NULL,
                    [RelatedArticleId] int NOT NULL,
                    [SortOrder] int NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_ArticleRelations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ArticleRelations_Articles_ArticleId]
                        FOREIGN KEY ([ArticleId]) REFERENCES [Articles] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ArticleRelations_Articles_RelatedArticleId]
                        FOREIGN KEY ([RelatedArticleId]) REFERENCES [Articles] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_ArticleRelations_ArticleId_RelatedArticleId]
                    ON [ArticleRelations] ([ArticleId], [RelatedArticleId]);
            END");
    }
}
