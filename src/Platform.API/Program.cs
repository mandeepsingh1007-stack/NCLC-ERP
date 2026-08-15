using Serilog;
using Serilog.Events;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using DbUp;
using Platform.Data.Repositories;
using Platform.Extensions;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Npgsql;
using Platform.API.Endpoints;
using Platform.Metadata.Factory;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/platform-.txt", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    Log.Information("Starting Platform API bootstrap");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog(Log.Logger, dispose: true);

    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Add Hangfire
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "No connection string 'Default' found. Configure PostgreSQL before starting.");
    builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer();

    // Add memory cache (metadata cache) — size-limited to prevent unbounded growth
    builder.Services.AddMemoryCache(options =>
    {
        // 100 MB hard limit on IMemoryCache entries
        options.SizeLimit = 100_000_000;
    });

    // Register Dictionary repositories (singleton — stateless Dapper repos, connection string resolved once at startup)
    builder.Services.AddSingleton<SysElementRepository>(sp => new SysElementRepository(connectionString));
    builder.Services.AddSingleton<SysTranslationRepository>(sp => new SysTranslationRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceRepository>(sp => new SysReferenceRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceListRepository>(sp => new SysReferenceListRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceTableRepository>(sp => new SysReferenceTableRepository(connectionString));
    builder.Services.AddSingleton<SysTableRepository>(sp => new SysTableRepository(connectionString));
    builder.Services.AddSingleton<SysColumnRepository>(sp => new SysColumnRepository(connectionString));
    builder.Services.AddSingleton<SysValRuleRepository>(sp => new SysValRuleRepository(connectionString));

    // Register UI metadata repositories (Phase 3)
    builder.Services.AddSingleton<SysWindowRepository>(sp => new SysWindowRepository(connectionString));
    builder.Services.AddSingleton<SysTabRepository>(sp => new SysTabRepository(connectionString));
    builder.Services.AddSingleton<SysFieldRepository>(sp => new SysFieldRepository(connectionString));
    builder.Services.AddSingleton<SysFieldGroupRepository>(sp => new SysFieldGroupRepository(connectionString));
    builder.Services.AddSingleton<SysMenuRepository>(sp => new SysMenuRepository(connectionString));

    // Add Redis for distributed caching / cache invalidation
    var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString")
        ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnection);

    var app = builder.Build();

    // Register Dapper type handlers for enum <-> string round-trip
    DapperTypeHandlers.Register();

    // Run database migrations via DbUp
    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
    {
        throw new InvalidOperationException($"Database migration failed: {result.Error}");
    }

    // Configure pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Phase 2: Register metadata runtime services
    var metadataConnectionString = connectionString;

    // IMetadataGraph — singleton, loads all metadata at construction
    builder.Services.AddSingleton<IMetadataGraph>(sp =>
        new MetadataGraph(metadataConnectionString));

    // Register all Platform runtime services (cache, validators, etc.)
    // IPOFactory and POLifecycleManager are registered separately as they depend on Platform.Metadata
    builder.Services.AddPlatformRuntime(redisConnection);

    // Override ValRuleEngine registration with table allowlist from MetadataGraph
    // Remove the default ValRuleEngine registration from AddPlatformRuntime
    var valRuleDesc = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IValRuleEngine));
    if (valRuleDesc != null)
    {
        builder.Services.Remove(valRuleDesc);
    }

    // Register ValRuleEngine with table allowlist from MetadataGraph
    builder.Services.AddTransient<IValRuleEngine, ValRuleEngine>(sp =>
    {
        var graph = sp.GetRequiredService<IMetadataGraph>();
        var connStr = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("No connection string 'Default' found.");
        var tables = graph.GetTableNames();
        return new ValRuleEngine(connStr, tables);
    });

    // IPOFactory — singleton
    builder.Services.AddSingleton<IPOFactory, POFactory>();

    // POLifecycleManager — transient
    builder.Services.AddTransient<POLifecycleManager>();

    // Phase 3: WindowMetadataBuilder — transient (reads from IMetadataGraph)
    builder.Services.AddTransient<IWindowMetadataBuilder, WindowMetadataBuilder>();

    // Phase 3: Register QueryBuilder (scoped — reads from singleton IMetadataGraph)
    builder.Services.AddScoped<QueryBuilder>();

    // Phase 3: Register NpgsqlConnection (scoped — one connection per request)
    builder.Services.AddScoped<NpgsqlConnection>(sp =>
        new NpgsqlConnection(connectionString));

    // Phase 3: Register null IReadOnlyContext (Phase 4 wires from JWT)
    builder.Services.AddScoped<IReadOnlyContext>(sp =>
        InMemoryContext.Create(null, null, null));

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    // Phase 3: Register generic API endpoints
    app.MapGenericDataEndpoints();
    app.MapGenericMetaEndpoints();
    app.MapGenericLookupEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
