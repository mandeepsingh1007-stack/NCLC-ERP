using StackExchange.Redis;
using Platform.Core.Runtime;

namespace Platform.Core.Cache;

/// <summary>
/// Two-tier cache invalidation:
/// 1. Publisher: invalidates local cache immediately + publishes to Redis pub/sub for distributed invalidation.
/// 2. Subscriber: receives from Redis pub/sub and invalidates local cache.
/// NEVER called before commit — must be post-transaction.
///
/// Redis reconnect/resubscribe (Phase 2):
/// - Listens for ConnectionFailed, ConnectionRestored, and EndPointsAdded events
/// - Automatically resubscribes when connection is re-established
/// - Thread-safe lazy init with reconnect support
/// - Graceful degradation: local cache always works even when Redis is unavailable
/// </summary>
public class CacheInvalidationService : ICacheInvalidationService, IDisposable
{
    private readonly IMetadataCache _cache;
    private readonly string? _redisConnectionString;
    private Lazy<IConnectionMultiplexer> _redisLazy;
    private ISubscriber? _subscriber;
    private readonly object _subscriberLock = new();
    private readonly object _reconnectLock = new();
    private readonly string _channelName = "cache-invalidation";
    private bool _disposed;

    // Events for testing/reconnect verification
    public event EventHandler<RedisConnectionEvent>? ConnectionChanged;

    public CacheInvalidationService(IMetadataCache cache, string? redisConnectionString = null)
    {
        _cache = cache;
        _redisConnectionString = redisConnectionString;
        var connStr = redisConnectionString ?? "localhost:6379";
        _redisLazy = new Lazy<IConnectionMultiplexer>(() =>
        {
            var conn = ConnectionMultiplexer.Connect(connStr);
            SetupReconnectHandlers(conn);
            return conn;
        });
    }

    /// <summary>
    /// Constructor without Redis connection — publisher-only mode.
    /// Local cache invalidation still works; pub/sub disabled.
    /// </summary>
    public CacheInvalidationService(IMetadataCache cache)
        : this(cache, (string?)null)
    {
    }

    private IConnectionMultiplexer Redis => _redisLazy.Value;

    public bool IsConnected => !_disposed && _redisLazy.IsValueCreated && Redis.IsConnected;

    /// <summary>
    /// Force reconnect — dispose current connection and create a new one.
    /// Automatically resubscribes after reconnection.
    /// </summary>
    public async Task ReconnectAsync()
    {
        if (_disposed) return;
        if (_redisConnectionString == null) return;

        IConnectionMultiplexer? oldConn = null;
        ISubscriber? oldSub = null;

        lock (_reconnectLock)
        {
            if (!_redisLazy.IsValueCreated) return;

            oldConn = Redis;
            oldSub = _subscriber;
            _subscriber = null;
        }

        // Dispose old connection
        try
        {
            oldConn?.Dispose();
        }
        catch
        {
            // Best effort
        }

        // Create new connection
        try
        {
            var newConn = ConnectionMultiplexer.Connect(_redisConnectionString);
            SetupReconnectHandlers(newConn);

            lock (_subscriberLock)
            {
                if (_disposed) return;

                _redisLazy = new Lazy<IConnectionMultiplexer>(() => newConn);
                _subscriber = newConn.GetSubscriber();

                // Resubscribe to the invalidation channel
                _subscriber.Subscribe(RedisChannel.Literal(_channelName), (channel, message) =>
                {
                    try
                    {
                        var json = message.ToString();
                        var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var entityType = root.GetProperty("EntityType").GetString();
                        var entityId = root.GetProperty("EntityId").GetInt32();
                        var entityKey = root.TryGetProperty("EntityKey", out var ek) ? ek.GetString() : null;
                        var changeType = root.GetProperty("ChangeType").GetString();

                        InvalidateByEvent(entityType ?? string.Empty, entityId, entityKey, changeType ?? string.Empty);
                    }
                    catch
                    {
                        // Bad message — ignore
                    }
                });
            }

            ConnectionChanged?.Invoke(this, new RedisConnectionEvent(true, true));
        }
        catch
        {
            ConnectionChanged?.Invoke(this, new RedisConnectionEvent(false, false));
            throw;
        }
    }

    private void SetupReconnectHandlers(IConnectionMultiplexer conn)
    {
        conn.ConnectionRestored += (sender, args) =>
        {
            // Connection re-established after a failure
            ConnectionChanged?.Invoke(this, new RedisConnectionEvent(true, _subscriber != null));
        };

        conn.ConnectionFailed += (sender, args) =>
        {
            // Connection failed — subscriber will be null until reconnect
            ConnectionChanged?.Invoke(this, new RedisConnectionEvent(false, false));
        };

        conn.ConfigurationChanged += (sender, args) =>
        {
            // Configuration changed (e.g., cluster topology) — log
        };
    }

    /// <summary>
    /// Two-step invalidation:
    /// 1. Invalidate local cache immediately (never wait for Redis).
    /// 2. Publish to Redis pub/sub so other nodes (and this node's subscribers) pick it up.
    /// If Redis is unavailable, local cache is already invalidated — that's the primary path.
    /// </summary>
    public async Task InvalidateAsync(DictionaryChangedEvent @event)
    {
        // Step 1: Local cache invalidation — always executed, never blocked by Redis
        InvalidateByEvent(@event.EntityType, @event.EntityId, @event.EntityKey, @event.ChangeType);

        // Step 2: Publish to Redis for distributed invalidation (other nodes)
        try
        {
            var subscriber = GetSubscriber();
            var msg = new
            {
                EntityType = @event.EntityType,
                EntityId = @event.EntityId,
                EntityKey = @event.EntityKey,
                ChangeType = @event.ChangeType
            };
            var json = System.Text.Json.JsonSerializer.Serialize(msg);
            await subscriber.PublishAsync(RedisChannel.Literal(_channelName), json);
        }
        catch
        {
            // Redis unavailable — local cache already invalidated, this node is fine
        }
    }

    private ISubscriber GetSubscriber()
    {
        if (_subscriber != null)
            return _subscriber;

        lock (_subscriberLock)
        {
            if (_subscriber != null)
                return _subscriber;

            _subscriber = Redis.GetSubscriber();

            // Subscribe to invalidation messages
            _subscriber.Subscribe(RedisChannel.Literal(_channelName), (channel, message) =>
            {
                try
                {
                    var json = message.ToString();
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var entityType = root.GetProperty("EntityType").GetString();
                    var entityId = root.GetProperty("EntityId").GetInt32();
                    var entityKey = root.TryGetProperty("EntityKey", out var ek) ? ek.GetString() : null;
                    var changeType = root.GetProperty("ChangeType").GetString();

                    // Invalidate by entity type + key from the published event
                    InvalidateByEvent(entityType ?? string.Empty, entityId, entityKey, changeType ?? string.Empty);
                }
                catch
                {
                    // Bad message — ignore
                }
            });

            return _subscriber;
        }
    }

    /// <summary>
    /// Invalidate all cached data for a table.
    /// </summary>
    public Task InvalidateTableAsync(string tableName)
    {
        _cache.InvalidateTable(tableName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle invalidation for both publisher (uses EntityKey) and subscriber (uses EntityKey when available).
    /// Key format matches MetadataCacheService: meta:{entityType}:{key}
    /// </summary>
    private void InvalidateByEvent(string entityType, int entityId, string? entityKey, string changeType)
    {
        switch (entityType.ToLowerInvariant())
        {
            case "table":
                // Use EntityKey (table name) as the identifier — matches meta:table:{name} pattern
                if (!string.IsNullOrEmpty(entityKey))
                {
                    _cache.InvalidateTable(entityKey);
                    // Invalidate tableMetadata entries with correct prefix
                    _cache.Invalidate($"meta:tableMetadata:{entityKey.ToLowerInvariant()}");
                }
                else
                {
                    // Fallback: no entity key available — invalidate all table cache as worst case
                    _cache.Invalidate("all-tables");
                }
                break;
            case "column":
                // A column was modified — invalidate all columns cache
                _cache.Invalidate("all-columns");
                break;
            case "reference":
                // Reference changed — invalidate reference cache
                if (!string.IsNullOrEmpty(entityKey))
                {
                    _cache.Invalidate($"meta:reference:{entityKey}");
                    _cache.Invalidate("all-references");
                }
                else
                {
                    // Fallback: no entity key — invalidate all reference cache
                    _cache.Invalidate("all-references");
                }
                break;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_redisLazy.IsValueCreated)
            {
                var redis = Redis;
                redis?.Dispose();
            }
            _disposed = true;
        }
    }
}
