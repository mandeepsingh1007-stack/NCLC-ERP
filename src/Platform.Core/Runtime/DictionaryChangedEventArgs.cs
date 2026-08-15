using Platform.Core.Metadata;

namespace Platform.Core.Runtime;

/// <summary>
/// Event args raised on the IMetadataGraph.DictionaryChanged event.
/// </summary>
public sealed class DictionaryChangedEventArgs : EventArgs
{
    public string EntityType { get; }
    public int EntityId { get; }
    public string ChangeType { get; }

    public DictionaryChangedEventArgs(string entityType, int entityId, string changeType)
    {
        EntityType = entityType;
        EntityId = entityId;
        ChangeType = changeType;
    }
}
