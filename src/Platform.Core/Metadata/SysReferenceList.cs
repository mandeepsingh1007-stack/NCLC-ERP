namespace Platform.Core.Metadata;

/// <summary>
/// A value entry within a SysReference of type LIST.
/// </summary>
public class SysReferenceList : ISysEntity
{
    public int SysReferenceListId { get; set; }
    public int SysReferenceId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SeqNo { get; set; }
    public bool IsActive { get; set; } = true;
}
