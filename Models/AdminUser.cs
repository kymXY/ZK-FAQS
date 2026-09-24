namespace FaqCms.Models;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string Role { get; set; } = "Editor";
    public bool IsPermanent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public List<AuditLogEntry> AuditLogs { get; set; } = new();
}

public enum AdminRole
{
    SuperAdmin = 1,
    Admin = 2,
    Editor = 3,
    Viewer = 4
}
