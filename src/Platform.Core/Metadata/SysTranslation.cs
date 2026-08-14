namespace Platform.Core.Metadata;

/// <summary>
/// Translation for a SysElement in a specific language.
/// Composite key: (SysElementId, LanguageCode).
/// </summary>
public class SysTranslation : ISysEntity
{
    public int SysElementId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Help { get; set; }
}
