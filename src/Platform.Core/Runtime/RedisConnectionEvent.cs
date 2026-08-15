using System;

namespace Platform.Core.Runtime;

/// <summary>
/// Event args for Redis connection state changes.
/// Used by CacheInvalidationService to notify subscribers of reconnect/resubscribe events.
/// </summary>
public class RedisConnectionEvent : EventArgs
{
    /// <summary>True if the connection is currently alive.</summary>
    public bool IsConnected { get; }

    /// <summary>True if the subscriber has been (re)established.</summary>
    public bool IsSubscribed { get; }

    public RedisConnectionEvent(bool isConnected, bool isSubscribed)
    {
        IsConnected = isConnected;
        IsSubscribed = isSubscribed;
    }
}
