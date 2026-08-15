namespace Platform.Core.Metadata;

/// <summary>
/// A reference type used for column validation (list/table/search).
/// </summary>
public class SysReference : ISysEntity
{
    public int SysReferenceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ValidationTypeEnum ValidationType { get; set; }
    public bool IsSystemType { get; set; }
    public string? ValueFormat { get; set; }
    public bool IsActive { get; set; } = true;
}
