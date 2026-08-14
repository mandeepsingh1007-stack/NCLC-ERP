namespace Platform.Core.Metadata;

/// <summary>
/// Base dictionary entity. Every translatable dictionary item is a SysElement.
/// </summary>
public class SysElement : ISysEntity
{
    public int SysElementId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Help { get; set; }
    public bool IsActive { get; set; } = true;
}
