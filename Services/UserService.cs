using FaqCms.Data;
using FaqCms.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FaqCms.Services;

public class UserService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly PasswordHasher<AdminUser> _hasher = new();

    public UserService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<AdminUser>> GetAllAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.AdminUsers.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<AdminUser?> GetUserByUsernameAsync(string username)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<string> GetUserRoleAsync(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Viewer";
        var user = await GetUserByUsernameAsync(username);
        return user?.Role ?? "Viewer";
    }

    public async Task<int> CountAsync()
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.AdminUsers.CountAsync();
    }

    /// <summary>Validates a login attempt against the stored (hashed) credentials.</summary>
    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !user.IsActive) return false;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>Creates a new admin account. Returns null if the username is already taken.</summary>
    public async Task<AdminUser?> CreateAsync(string username, string password, string? email = null, string? fullName = null, string role = "Editor", string? createdBy = null)
    {
        username = username.Trim();
        using var db = _dbFactory.CreateDbContext();

        var exists = await db.AdminUsers.AnyAsync(u => u.Username == username);
        if (exists) return null;

        var user = new AdminUser 
        { 
            Username = username,
            Email = email,
            FullName = fullName,
            Role = role
        };
        user.PasswordHash = _hasher.HashPassword(user, password);

        db.AdminUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(AdminUser user, string updatedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var existing = await db.AdminUsers.FindAsync(user.Id);
        if (existing == null) return;

        existing.Email = user.Email;
        existing.FullName = user.FullName;
        existing.Role = user.Role;
        existing.IsActive = user.IsActive;
        
        await db.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(int userId, string newPassword, string? changedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = await db.AdminUsers.FindAsync(userId);
        if (user is null) return;

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RecordLoginAsync(string username, string ipAddress, string userAgent)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Deletes an admin account. Refuses to delete the last remaining account
    /// (so the CMS can never be locked out entirely) or any account marked permanent.</summary>
    public async Task<bool> DeleteAsync(int userId, string? deletedBy = null)
    {
        using var db = _dbFactory.CreateDbContext();
        var total = await db.AdminUsers.CountAsync();
        if (total <= 1) return false;

        var user = await db.AdminUsers.FindAsync(userId);
        if (user is null || user.IsPermanent) return false;

        db.AdminUsers.Remove(user);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Creates the first admin account from config if the table is empty —
    /// keeps the app usable on a fresh database without a manual setup step.</summary>
    public async Task EnsureSeedAdminAsync(string username, string password)
    {
        using var db = _dbFactory.CreateDbContext();
        if (await db.AdminUsers.AnyAsync()) return;

        var user = new AdminUser { Username = username, Role = "SuperAdmin" };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLogEntry>> GetAuditLogAsync(int? userId = null, int limit = 100)
    {
        using var db = _dbFactory.CreateDbContext();
        var query = db.AuditLog.OrderByDescending(a => a.Timestamp).AsQueryable();
        if (userId.HasValue)
            query = query.Where(a => a.AdminUserId == userId.Value);
        return await query.Take(limit).ToListAsync();
    }
}
