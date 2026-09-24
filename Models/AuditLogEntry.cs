namespace FaqCms.Models;

public class AuditLogEntry
{
    public int Id { get; set; }

    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public string Username { get; set; } = "system";
    public string? ChangesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int? AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Published = 4,
    Unpublished = 5,
    Login = 6,
    Logout = 7,
    PasswordChanged = 8,
    Restored = 9
}

public enum AuditEntityType
{
    Article = 1,
    Category = 2,
    User = 3,
    Tag = 4,
    Feedback = 5
}
