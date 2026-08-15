namespace Platform.Core.Metadata;

/// <summary>
/// A field definition within a tab — maps a UI field to a data column (sys_column_id).
/// Contains override flags for mandatory/read-only, display logic, and layout info.
/// </summary>
public class SysField : ISysEntity
{
    public int SysFieldId { get; set; }
    public int SysTabId { get; set; }
    public int SysColumnId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ControlType { get; set; } = string.Empty;
    public int? SysFieldGroupId { get; set; }
    public int SeqNo { get; set; }
    public bool IsMandatoryOverride { get; set; }
    public bool IsReadOnlyOverride { get; set; }
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string? DisplayLogic { get; set; }
    public string? ReadOnlyLogic { get; set; }
    public string? MandatoryLogic { get; set; }
    public string? DefaultValue { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
