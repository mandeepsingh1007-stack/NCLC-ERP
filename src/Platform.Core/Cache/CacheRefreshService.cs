using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Core.Cache;

/// <summary>
/// Background worker that proactively refreshes cache entries before TTL expiry.
/// Checks every 5 minutes and refreshes entries that will expire within 5 minutes.
/// </summary>
public class CacheRefreshService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheRefreshService> _logger;
    private Timer? _timer;
    private const int CheckIntervalMinutes = 5;
    private const int RefreshThresholdMinutes = 5;
    private bool _disposed;

    public CacheRefreshService(IServiceProvider serviceProvider, ILogger<CacheRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CacheRefreshService starting — checking every {Interval}m, threshold {Threshold}m",
            CheckIntervalMinutes, RefreshThresholdMinutes);
        _timer = new Timer(DoCheck, null,
            TimeSpan.FromMinutes(CheckIntervalMinutes),
            TimeSpan.FromMinutes(CheckIntervalMinutes));
        return Task.CompletedTask;
    }

    private void DoCheck(object? state)
    {
        try
        {
            _logger.LogDebug("CacheRefreshService checking for entries near expiry");
            // Phase 2: cache refresh is a nice-to-have. The actual refresh logic
            // will be implemented when the metadata graph supports partial reloads.
            // For now, this service runs but has no entries to refresh.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CacheRefreshService check failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}
