using Api.Domain.Location.Config;
using Api.Domain.Location.Realtime;
using Api.Domain.Location.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Api.Global.Security;
using Api.Global.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Prometheus;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleAzureMonitor();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SchemaFilter<SwaggerDefaultValueSchemaFilter>();
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Tangle API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    // YAML is loaded after the host's default env vars; re-add so deploy secrets win.
    .AddEnvironmentVariables();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.Configure<LocationSafetyOptions>(
    builder.Configuration.GetSection(LocationSafetyOptions.SectionName));
builder.Services.Configure<LocationClusterOptions>(
    builder.Configuration.GetSection(LocationClusterOptions.SectionName));
builder.Services.AddHostedService<LocationSafetyMonitorHostedService>();
builder.Services.AddSingleton<TokenProvider>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(sp =>
    new PostConfigureOptions<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            var tokenProvider = sp.GetRequiredService<TokenProvider>();
            options.TokenValidationParameters = tokenProvider.GetValidationParameters();
            options.MapInboundClaims = false;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken)
                        && path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        context.Token = accessToken;

                    return Task.CompletedTask;
                },
            };
        }));

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("places", context =>
    {
        var placesOptions = context.RequestServices.GetRequiredService<IOptions<PlacesOptions>>().Value;
        if (placesOptions.RateLimitPerMinute <= 0)
        {
            return RateLimitPartition.GetNoLimiter("places-disabled");
        }

        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = placesOptions.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
            });
    });
    options.AddPolicy("location-clusters", context =>
    {
        var clusterOptions = context.RequestServices.GetRequiredService<IOptions<LocationClusterOptions>>().Value;
        if (clusterOptions.RateLimitPerMinute <= 0)
            return RateLimitPartition.GetNoLimiter("location-clusters-disabled");

        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = clusterOptions.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
            });
    });
});
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleWorkerCallbackAuth(builder.Configuration);
builder.Services.AddTangleMediaClient(builder.Configuration);
builder.Services.AddTangleChatClient(builder.Configuration);
builder.Services.AddTanglePlaces(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres");

var redisConfig = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
if (redisConfig?.Enabled is true && !string.IsNullOrWhiteSpace(redisConfig.ConnectionString))
{
    healthChecksBuilder.AddRedis(
        redisConfig.ConnectionString,
        name: "redis",
        timeout: TimeSpan.FromSeconds(5));
}

healthChecksBuilder.ForwardToPrometheus();

var app = builder.Build();

var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions);

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

var redisOptions = app.Services.GetRequiredService<IOptions<RedisOptions>>().Value;
if (redisOptions.Enabled) logger.LogInformation("Redis enabled (cache + SignalR backplane).");
else logger.LogInformation("Redis disabled; using in-memory distributed cache and in-process SignalR.");

var placesOptions = app.Services.GetRequiredService<IOptions<PlacesOptions>>().Value;
if (placesOptions.Enabled && !string.IsNullOrWhiteSpace(placesOptions.ApiKey))
    logger.LogInformation("Google Places search enabled.");
else logger.LogInformation("Google Places search disabled (set Places:Enabled and Places:ApiKey).");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle API v1");
        options.RoutePrefix = "api";
    });

    // Production applies migrations via `dotnet Api.dll --migrate` (see scripts/migrate.sh).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseRouting();
app.UseRateLimiter();
app.UseHttpMetrics();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<MetricsScrapeAuthMiddleware>();

app.MapControllers();
app.MapHub<LocationHub>("/hubs/location");
app.MapHealthChecks("/health");
app.MapMetrics();

app.Run();