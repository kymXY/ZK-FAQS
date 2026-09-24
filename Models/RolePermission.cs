namespace FaqCms.Models;

/// <summary>
/// One cell of the role/permission matrix: whether a given role is allowed
/// to perform a given action. Rows are seeded with sensible defaults on
/// first run (see PermissionService.EnsureSchemaAndDefaultsAsync) and are
/// then editable from /admin/roles by a SuperAdmin.
///
/// "SuperAdmin" is intentionally never represented here — it always has
/// every permission, hardcoded in PermissionService, so there's no way to
/// accidentally lock every admin out of the panel by unchecking a box.
/// </summary>
public class RolePermission
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string PermissionKey { get; set; } = "";
    public bool Allowed { get; set; }
}
