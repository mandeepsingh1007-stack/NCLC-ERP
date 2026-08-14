namespace Platform.Core.Metadata;

/// <summary>
/// A dictionary table definition. Each row represents a business table the platform can manage.
/// </summary>
public class SysTable : ISysEntity
{
    public int SysTableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? Description { get; set; }
    public bool IsView { get; set; }
    public short AccessLevel { get; set; } = 3;
    public bool IsChangeLog { get; set; }
    public bool IsDeleteable { get; set; }
    public bool IsHighVolume { get; set; }
    public string? ReplicationType { get; set; }
    public int? SysWindowId { get; set; }
    public string EntityType { get; set; } = "D";
    public bool IsActive { get; set; } = true;
}
