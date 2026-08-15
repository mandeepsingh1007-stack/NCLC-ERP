namespace Platform.Core.Metadata;

/// <summary>
/// A collapsible field group/section within a tab (e.g., "Basic Info", "Address").
/// Fields can be assigned to groups for organized form layout.
/// </summary>
public class SysFieldGroup : ISysEntity
{
    public int SysFieldGroupId { get; set; }
    public int SysTabId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SeqNo { get; set; }
    public int ColSpan { get; set; } = 12;
    public bool IsCollapsed { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
