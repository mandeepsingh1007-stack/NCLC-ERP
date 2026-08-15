namespace Platform.Core.Metadata;

/// <summary>
/// A tab within a window, bound to a data table (sys_table_id).
/// Tabs contain fields for form rendering or grids for data display.
/// </summary>
public class SysTab : ISysEntity
{
    public int SysTabId { get; set; }
    public int SysWindowId { get; set; }
    public int SysTableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SeqNo { get; set; }
    public bool IsDefaultTab { get; set; }
    public bool IsGrid { get; set; }
    public string? WhereClause { get; set; }
    public bool IsDeleteable { get; set; } = true;
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
