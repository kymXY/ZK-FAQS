using FaqCms.Data;
using FaqCms.Models;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Services;

public class SettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>Creates the HelpdeskSettings table and its one row if they don't exist yet.
    /// Safe to call on every startup — same pattern as PermissionService.</summary>
    public async Task EnsureSchemaAndDefaultsAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'[HelpdeskSettings]') IS NULL
            BEGIN
                CREATE TABLE [HelpdeskSettings] (
                    [Id] int NOT NULL,
                    [DemoVideoUrl] nvarchar(max) NULL,
                    [DemoVideoTitle] nvarchar(max) NOT NULL,
                    [PayrollSummaryTitle] nvarchar(max) NOT NULL,
                    [PayrollSummaryBody] nvarchar(max) NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_HelpdeskSettings] PRIMARY KEY ([Id])
                );
            END");

        if (await db.HelpdeskSettings.AnyAsync()) return;

        db.HelpdeskSettings.Add(new HelpdeskSettings());
        await db.SaveChangesAsync();
    }

    public async Task<HelpdeskSettings> GetAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.HelpdeskSettings.FirstOrDefaultAsync(s => s.Id == 1) ?? new HelpdeskSettings();
    }

    public async Task SaveAsync(HelpdeskSettings settings)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.HelpdeskSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (existing is null)
        {
            settings.Id = 1;
            settings.UpdatedAt = DateTime.UtcNow;
            db.HelpdeskSettings.Add(settings);
        }
        else
        {
            existing.DemoVideoUrl = settings.DemoVideoUrl;
            existing.DemoVideoTitle = settings.DemoVideoTitle;
            existing.PayrollSummaryTitle = settings.PayrollSummaryTitle;
            existing.PayrollSummaryBody = settings.PayrollSummaryBody;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
