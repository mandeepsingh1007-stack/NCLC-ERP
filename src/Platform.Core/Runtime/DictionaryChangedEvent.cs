using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Domain event published after a successful metadata transaction commit.
/// Triggers cache invalidation at both node-local and distributed (Redis) levels.
/// </summary>
public sealed class DictionaryChangedEvent
{
    public string EntityType { get; }
    public int EntityId { get; }
    public string EntityKey { get; }
    public string ChangeType { get; }

    public DictionaryChangedEvent(string entityType, int entityId, string entityKey, string changeType)
    {
        EntityType = entityType;
        EntityId = entityId;
        EntityKey = entityKey;
        ChangeType = changeType;
    }
}
