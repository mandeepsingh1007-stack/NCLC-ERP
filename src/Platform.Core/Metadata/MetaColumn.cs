namespace Platform.Core.Metadata;

/// <summary>
/// Runtime composition of SysColumn + SysElement + SysReference + SysValRule.
/// NOT persisted — built dynamically from database queries.
/// </summary>
public class MetaColumn
{
    public int SysColumnId { get; set; }
    public int SysTableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Help { get; set; }
    public string BaseType { get; set; } = string.Empty;
    public string? ValidationType { get; set; }
    public int? SysReferenceId { get; set; }
    public int? SysValRuleId { get; set; }
    public ValRuleTypeEnum ValRuleType { get; set; }
    public string? ValRuleCode { get; set; }
    public int? FieldLength { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsKey { get; set; }
    public bool IsUpdateable { get; set; }
    public string? ValueMin { get; set; }
    public string? ValueMax { get; set; }
    public string? DefaultValue { get; set; }
    public string? ReferenceName { get; set; }
    public int SeqNo { get; set; }
    public bool IsActive { get; set; } = true;
}
