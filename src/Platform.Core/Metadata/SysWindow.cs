namespace Platform.Core.Metadata;

/// <summary>
/// A top-level window definition (e.g., "Library Book", "User Management").
/// Windows contain tabs, which contain fields mapped to data columns.
/// </summary>
public class SysWindow : ISysEntity
{
    public int SysWindowId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Help { get; set; }
    public int? DefaultTabId { get; set; }
    public short AccessLevel { get; set; } = 3;
    public bool IsView { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
