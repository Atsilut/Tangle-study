using Location.Config;
using Location.Db;
using Location.Infrastructure;
using Location.Realtime;
using Location.Service;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using System.Threading.RateLimiting;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;
using Location.Security;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync<LocationDbContext>(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleServiceDefaults("Tangle Location API");

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("location-config.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<LocationSafetyOptions>(builder.Configuration.GetSection(LocationSafetyOptions.SectionName));
builder.Services.Configure<LocationClusterOptions>(builder.Configuration.GetSection(LocationClusterOptions.SectionName));
builder.Services.Configure<PlacesOptions>(builder.Configuration.GetSection(PlacesOptions.SectionName));
builder.Services.Configure<WorkerCallbackOptions>(builder.Configuration.GetSection(WorkerCallbackOptions.SectionName));
builder.Services.AddScoped<WorkerCallbackAuthorizationFilter>();
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleUsersAccess(builder.Configuration);
builder.Services.AddTangleSocialClient(builder.Configuration);
builder.Services.AddTangleGroupClient(builder.Configuration);
builder.Services.AddTangleCommunityAccess(builder.Configuration);
builder.Services.AddTanglePlaces(builder.Configuration);
builder.Services.AddHostedService<LocationSafetyMonitorHostedService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
    options.AddPolicy("places", context =>
    {
        var placesOptions = context.RequestServices.GetRequiredService<IOptions<PlacesOptions>>().Value;
        if (placesOptions.RateLimitPerMinute <= 0)
            return RateLimitPartition.GetNoLimiter("places-disabled");

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
});

builder.Services.AddDbContext<LocationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres");

var redisConfig = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
    ?? throw new InvalidOperationException("Redis configuration section is missing.");
RedisStartupValidator.Validate(redisConfig);

var locationSafetyConfig = builder.Configuration.GetSection(LocationSafetyOptions.SectionName).Get<LocationSafetyOptions>()
    ?? throw new InvalidOperationException("LocationSafety configuration section is missing.");
var locationClusterConfig = builder.Configuration.GetSection(LocationClusterOptions.SectionName).Get<LocationClusterOptions>()
    ?? throw new InvalidOperationException("LocationCluster configuration section is missing.");
var placesConfig = builder.Configuration.GetSection(PlacesOptions.SectionName).Get<PlacesOptions>()
    ?? throw new InvalidOperationException("Places configuration section is missing.");
LocationConfigStartupValidator.Validate(locationSafetyConfig, locationClusterConfig, placesConfig);
healthChecksBuilder.AddRedis(
    redisConfig.ConnectionString,
    name: "redis",
    timeout: TimeSpan.FromSeconds(5));

healthChecksBuilder.ForwardToPrometheus();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

logger.LogInformation("Redis configured (SignalR backplane, work queue, distributed cache).");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LocationDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the location schema database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseTangleServicePipeline("Tangle Location API");
app.UseRateLimiter();
app.MapTangleServiceEndpoints();
app.MapHub<LocationHub>("/hubs/location");

app.Run();

public partial class Program { }
