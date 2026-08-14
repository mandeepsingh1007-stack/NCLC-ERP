using Serilog;
using Serilog.Events;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using DbUp;
using Platform.Data.Repositories;
using Platform.Core.Metadata;

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
    builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(connectionString));
    builder.Services.AddHangfireServer();

    // Add memory cache (metadata cache)
    builder.Services.AddMemoryCache();

    // Register Dictionary repositories (singleton — stateless Dapper repos, connection string resolved once at startup)
    builder.Services.AddSingleton<SysElementRepository>(sp => new SysElementRepository(connectionString));
    builder.Services.AddSingleton<SysTranslationRepository>(sp => new SysTranslationRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceRepository>(sp => new SysReferenceRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceListRepository>(sp => new SysReferenceListRepository(connectionString));
    builder.Services.AddSingleton<SysReferenceTableRepository>(sp => new SysReferenceTableRepository(connectionString));
    builder.Services.AddSingleton<SysTableRepository>(sp => new SysTableRepository(connectionString));
    builder.Services.AddSingleton<SysColumnRepository>(sp => new SysColumnRepository(connectionString));
    builder.Services.AddSingleton<SysValRuleRepository>(sp => new SysValRuleRepository(connectionString));

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

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

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
