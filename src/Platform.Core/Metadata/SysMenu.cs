namespace Platform.Core.Metadata;

/// <summary>
/// A hierarchical navigation menu item. Self-referencing via parent_id.
/// Links to windows (for navigation) or processes (for action items).
/// </summary>
public class SysMenu : ISysEntity
{
    public int SysMenuId { get; set; }
    public int? ParentId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int Sequence { get; set; }
    public int? WindowId { get; set; }
    public int? ProcessId { get; set; }
    public bool IsSeparator { get; set; }
    public bool IsSystem { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
