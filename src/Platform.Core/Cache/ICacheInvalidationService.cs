using Platform.Core.Runtime;

namespace Platform.Core.Cache;

/// <summary>
/// Publishes DictionaryChangedEvent to Redis pub/sub after successful commit.
/// Subscribers (other nodes and local cache) pick up the message and invalidate.
/// NEVER called before commit — must be post-transaction.
/// </summary>
public interface ICacheInvalidationService
{
    Task InvalidateAsync(DictionaryChangedEvent @event);
    Task InvalidateTableAsync(string tableName);
}
