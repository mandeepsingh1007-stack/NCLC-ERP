namespace Platform.Core.Metadata;

/// <summary>
/// A column definition within a SysTable. Links to SysElement, SysReference, and SysValRule.
/// </summary>
public class SysColumn : ISysEntity
{
    public int SysColumnId { get; set; }
    public int SysTableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public int? SysElementId { get; set; }
    public int SysReferenceId { get; set; }
    public int? SysValRuleId { get; set; }
    public int? SysReferenceValueId { get; set; }
    public int? FieldLength { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsKey { get; set; }
    public bool IsParent { get; set; }
    public bool IsIdentifier { get; set; }
    public bool IsSelectionColumn { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsUpdateable { get; set; }
    public bool IsAlwaysUpdateable { get; set; }
    public string? DefaultValue { get; set; }
    public string? ValueMin { get; set; }
    public string? ValueMax { get; set; }
    public int SeqNo { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
}
