using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using DbUp;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Platform.API.Endpoints;
using Platform.API.Services;
using Platform.Core.Auth;
using Platform.Core.Metadata;
using Platform.Core.Runtime;
using Platform.Data.Repositories;
using Platform.Extensions;
using Platform.Metadata.Factory;
using Serilog;
using Serilog.Events;

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

    // Register Dictionary repositories (singleton — stateless Dapper repos)
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

    // Register auth repositories (Phase 5)
    builder.Services.AddSingleton<SysUserRepository>(sp => new SysUserRepository(connectionString));

    // Add Redis for distributed caching / cache invalidation
    var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString")
        ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnection);

    // JWT Authentication (Phase 5)
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

    var jwtKey = jwtSettings.SecretKey;
    if (string.IsNullOrEmpty(jwtKey))
        throw new InvalidOperationException("JWT SecretKey must be configured in appsettings.json or environment variables.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Set the authorization header format
            options.Authority = string.Empty;
            options.Audience = jwtSettings.Audience;
            options.Events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    // Suppress the default WWW-Authenticate challenge for cleaner 401 responses
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    return context.Response.WriteAsJsonAsync(new
                    {
                        error = new { code = "Unauthorized", message = "Authentication required." }
                    });
                }
            };
        });

    builder.Services.AddAuthorization();

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

    // Enable authentication/authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Phase 2: Register metadata runtime services
    var metadataConnectionString = connectionString;

    // IMetadataGraph — singleton, loads all metadata at construction
    builder.Services.AddSingleton<IMetadataGraph>(sp =>
        new MetadataGraph(metadataConnectionString));

    // Register all Platform runtime services (cache, validators, etc.)
    builder.Services.AddPlatformRuntime(redisConnection);

    // Override ValRuleEngine registration with table allowlist from MetadataGraph
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

    // Phase 3: Register QueryBuilder (scoped)
    builder.Services.AddScoped<QueryBuilder>();

    // Phase 3: Register NpgsqlConnection (scoped)
    builder.Services.AddScoped<NpgsqlConnection>(sp =>
        new NpgsqlConnection(connectionString));

    // Phase 5: Register auth services and JWT-based context
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<Platform.Core.Auth.IRbacRepository, Platform.Data.Repositories.RbacRepository>(sp =>
        new Platform.Data.Repositories.RbacRepository(connectionString));
    builder.Services.AddScoped<Platform.Core.Auth.INamespaceRepository>(sp =>
        new Platform.Data.Repositories.NamespaceRepository(connectionString));
    builder.Services.AddHttpContextAccessor();

    // Phase 5: Register IReadOnlyContext from JWT claims (with tenant isolation)
    builder.Services.AddScoped<IReadOnlyContext>(sp =>
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return InMemoryContext.Create(null, null, null);

        var user = httpContext.User;
        var userId = user.FindFirst(AuthClaimTypes.UserId)?.Value;
        var clientId = user.FindFirst(AuthClaimTypes.ClientId)?.Value;
        var orgId = user.FindFirst(AuthClaimTypes.OrgId)?.Value;

        // Build tenant predicates for QueryBuilder
        string? tenantPredicate = null;
        if (!string.IsNullOrEmpty(clientId))
            tenantPredicate = $"\"SysClient_ID\" = @ClientId";

        string? orgPredicate = null;
        if (!string.IsNullOrEmpty(orgId))
            orgPredicate = $"\"SysOrg_ID\" = @OrgId";

        return InMemoryContext.CreateWithTenantIsolation(
            userId,
            clientId,
            orgId,
            tenantPredicate,
            orgPredicate);
    });

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
        .AllowAnonymous();

    // Phase 5: Auth endpoints (no auth required for login/refresh)
    app.MapAuthEndpoints();

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
