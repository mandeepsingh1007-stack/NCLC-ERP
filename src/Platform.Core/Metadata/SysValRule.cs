namespace Platform.Core.Metadata;

/// <summary>
/// A validation rule that can be attached to a SysColumn.
/// </summary>
public class SysValRule : ISysEntity
{
    public int SysValRuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ValRuleTypeEnum RuleType { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
