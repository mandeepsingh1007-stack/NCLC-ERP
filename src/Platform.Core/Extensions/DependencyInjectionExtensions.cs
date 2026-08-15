using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Cache;
using Platform.Core.Metadata;
using Platform.Core.Runtime;

namespace Platform.Extensions;

/// <summary>
/// Extension methods for registering Platform metadata runtime services.
/// Placed in Platform.Core to avoid SDK 10 namespace resolution issues
/// with Microsoft.Extensions.Caching.Abstractions in Web SDK projects.
/// Uses Microsoft.Extensions.Caching.Distributed namespace for IDistributedCache
/// to bypass the Abstractions namespace resolution problem.
///
/// Note: IPOFactory, POLifecycleManager, and IMetadataGraph must be registered
/// separately in Platform.API since they depend on Platform.Metadata types.
/// </summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddPlatformRuntime(this IServiceCollection services, string redisConnectionString)
    {
        // IMetadataCache — singleton, two-layer cache
        services.AddSingleton<IMetadataCache>(sp =>
            new MetadataCacheService(
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IDistributedCache>()));

        // ICacheInvalidationService — singleton
        services.AddSingleton<ICacheInvalidationService>(sp =>
            new CacheInvalidationService(
                sp.GetRequiredService<IMetadataCache>(),
                redisConnectionString));

        // Note: IValRuleEngine is registered in Program.cs with table allowlist.
        // Do NOT register here — it requires (string connectionString, IEnumerable<string> allowedTables).

        // ITypeValidator — singleton (stateless)
        services.AddSingleton<ITypeValidator, TypeValidator>();

        // IReferenceValueValidator — singleton (stateless)
        services.AddSingleton<IReferenceValueValidator, ReferenceValueValidator>();

        // IContextVariableResolver — scoped (reads from HTTP context)
        services.AddScoped<IContextVariableResolver, ContextVariableResolver>();

        // CacheRefreshService — hosted service
        services.AddHostedService<CacheRefreshService>();

        // POValidator — transient (orchestrates validators)
        services.AddTransient<POValidator>();

        return services;
    }
}
