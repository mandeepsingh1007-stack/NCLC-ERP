using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Result of a lifecycle hook. Allowed=false means the operation is vetoed.
/// </summary>
public sealed class HookResult
{
    public bool Allowed { get; }
    public string? Message { get; }

    private HookResult(bool allowed, string? message)
    {
        Allowed = allowed;
        Message = message;
    }

    public static HookResult Allow() => new(true, null);
    public static HookResult Veto(string reason) => new(false, reason);
}
