namespace Platform.Core.Metadata;

/// <summary>
/// A table-based reference (foreign table lookup).
/// </summary>
public class SysReferenceTable : ISysEntity
{
    public int SysReferenceId { get; set; }
    public int SysTableId { get; set; }
    public string KeyColumn { get; set; } = string.Empty;
    public string DisplayColumn { get; set; } = string.Empty;
    public string? WhereClause { get; set; }
    public string? OrderByClause { get; set; }
}
