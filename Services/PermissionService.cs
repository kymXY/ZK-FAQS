using FaqCms.Data;
using FaqCms.Models;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Services;

/// <summary>Every action in the admin panel that can be turned on/off per role.</summary>
public static class Permissions
{
    public const string ArticlesManage = "Articles.Manage";
    public const string CategoriesManage = "Categories.Manage";
    public const string TagsManage = "Tags.Manage";
    public const string FeedbackManage = "Feedback.Manage";
    public const string UsersManage = "Users.Manage";
    public const string AuditLogView = "AuditLog.View";
    public const string SettingsManage = "Settings.Manage";

    /// <summary>Display metadata for the /admin/roles editor, in the order they should be shown.</summary>
    public static readonly (string Key, string Label, string Description)[] All =
    {
        (ArticlesManage, "Manage Articles", "Create, edit, publish, and delete knowledge-base articles."),
        (CategoriesManage, "Manage Categories", "Create, edit, and delete categories."),
        (TagsManage, "Manage Tags", "Create, edit, and delete tags."),
        (FeedbackManage, "Manage Feedback", "Toggle visibility of and delete visitor feedback."),
        (UsersManage, "Manage Admins", "Add, edit, remove admin accounts and change their passwords."),
        (AuditLogView, "View Audit Log", "See the history of who changed what."),
        (SettingsManage, "Manage Site Settings", "Edit the Helpdesk dashboard's demo video and payroll summary."),
    };

    /// <summary>The four fixed roles this app supports. SuperAdmin is deliberately not
    /// stored in the matrix — it always has every permission (see PermissionService).</summary>
    public static readonly string[] ConfigurableRoles = { "Admin", "Editor", "Viewer" };
}

public class PermissionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    // Small in-process cache so every page load doesn't hit the database just to
    // decide whether to show a "Delete" button. Invalidated on any SetAsync call.
    private static Dictionary<(string Role, string Key), bool>? _cache;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public PermissionService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    private static readonly (string Role, string Key, bool Allowed)[] Defaults =
    {
        ("Admin", Permissions.ArticlesManage, true),
        ("Admin", Permissions.CategoriesManage, true),
        ("Admin", Permissions.TagsManage, true),
        ("Admin", Permissions.FeedbackManage, true),
        ("Admin", Permissions.UsersManage, true),
        ("Admin", Permissions.AuditLogView, true),
        ("Admin", Permissions.SettingsManage, true),

        ("Editor", Permissions.ArticlesManage, true),
        ("Editor", Permissions.CategoriesManage, true),
        ("Editor", Permissions.TagsManage, true),
        ("Editor", Permissions.FeedbackManage, true),
        ("Editor", Permissions.UsersManage, false),
        ("Editor", Permissions.AuditLogView, false),
        ("Editor", Permissions.SettingsManage, true),

        ("Viewer", Permissions.ArticlesManage, false),
        ("Viewer", Permissions.CategoriesManage, false),
        ("Viewer", Permissions.TagsManage, false),
        ("Viewer", Permissions.FeedbackManage, false),
        ("Viewer", Permissions.UsersManage, false),
        ("Viewer", Permissions.AuditLogView, false),
        ("Viewer", Permissions.SettingsManage, false),
    };

    /// <summary>Creates the RolePermissions table if it doesn't exist yet and seeds the
    /// default matrix. Safe to call on every startup — mirrors UserService.EnsureSeedAdminAsync
    /// so a fresh database works without a manual migration step.</summary>
    public async Task EnsureSchemaAndDefaultsAsync()
    {
        using var db = _dbFactory.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'[RolePermissions]') IS NULL
            BEGIN
                CREATE TABLE [RolePermissions] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Role] nvarchar(450) NOT NULL,
                    [PermissionKey] nvarchar(450) NOT NULL,
                    [Allowed] bit NOT NULL,
                    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_RolePermissions_Role_PermissionKey] ON [RolePermissions] ([Role], [PermissionKey]);
            END");

        if (await db.RolePermissions.AnyAsync())
        {
            // Table already existed from an earlier version of this app — backfill only
            // whichever (role, permission) pairs are new since then (e.g. Settings.Manage),
            // rather than skipping seeding entirely just because some rows exist.
            var existing = await db.RolePermissions.Select(r => new { r.Role, r.PermissionKey }).ToListAsync();
            var existingSet = existing.Select(e => (e.Role, e.PermissionKey)).ToHashSet();
            var missing = Defaults.Where(d => !existingSet.Contains((d.Role, d.Key))).ToList();
            if (missing.Count == 0) return;

            foreach (var (role, key, allowed) in missing)
            {
                db.RolePermissions.Add(new RolePermission { Role = role, PermissionKey = key, Allowed = allowed });
            }
            await db.SaveChangesAsync();
            return;
        }

        foreach (var (role, key, allowed) in Defaults)
        {
            db.RolePermissions.Add(new RolePermission { Role = role, PermissionKey = key, Allowed = allowed });
        }
        await db.SaveChangesAsync();
    }

    private async Task<Dictionary<(string, string), bool>> LoadCacheAsync()
    {
        if (_cache is not null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;
            using var db = _dbFactory.CreateDbContext();
            var rows = await db.RolePermissions.ToListAsync();
            _cache = rows.ToDictionary(r => (r.Role, r.PermissionKey), r => r.Allowed);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void InvalidateCache() => _cache = null;

    /// <summary>SuperAdmin always has every permission — hardcoded, not stored, and not
    /// editable from the UI, so there's no way to accidentally lock every admin out.</summary>
    public async Task<bool> IsAllowedAsync(string? role, string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        if (role == "SuperAdmin") return true;

        var cache = await LoadCacheAsync();
        return cache.TryGetValue((role, permissionKey), out var allowed) && allowed;
    }

    /// <summary>Returns the full editable matrix (excludes SuperAdmin — see IsAllowedAsync) for /admin/roles.</summary>
    public async Task<Dictionary<string, Dictionary<string, bool>>> GetMatrixAsync()
    {
        var cache = await LoadCacheAsync();
        var matrix = new Dictionary<string, Dictionary<string, bool>>();
        foreach (var role in Permissions.ConfigurableRoles)
        {
            matrix[role] = new Dictionary<string, bool>();
            foreach (var (key, _, _) in Permissions.All)
            {
                matrix[role][key] = cache.TryGetValue((role, key), out var allowed) && allowed;
            }
        }
        return matrix;
    }

    public async Task SetAsync(string role, string permissionKey, bool allowed)
    {
        if (role == "SuperAdmin") return; // not stored; always true

        using var db = _dbFactory.CreateDbContext();
        var existing = await db.RolePermissions.FirstOrDefaultAsync(r => r.Role == role && r.PermissionKey == permissionKey);
        if (existing is null)
        {
            db.RolePermissions.Add(new RolePermission { Role = role, PermissionKey = permissionKey, Allowed = allowed });
        }
        else
        {
            existing.Allowed = allowed;
        }
        await db.SaveChangesAsync();
        InvalidateCache();
    }
}
