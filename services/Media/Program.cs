using Media.Config;
using Media.Db;
using Media.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync<MediaDbContext>(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleServiceDefaults("Tangle Media API");

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("media-config.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleMedia(builder.Configuration);
builder.Services.AddTangleUsersAccess(builder.Configuration);
builder.Services.AddTangleCommunityAccess(builder.Configuration);
builder.Services.AddTangleChatAccess(builder.Configuration);

builder.Services.AddDbContext<MediaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres");

var redisConfig = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
    ?? throw new InvalidOperationException("Redis configuration section is missing.");
RedisStartupValidator.Validate(redisConfig);
healthChecksBuilder.AddRedis(
    redisConfig.ConnectionString,
    name: "redis",
    timeout: TimeSpan.FromSeconds(5));

healthChecksBuilder.ForwardToPrometheus();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

logger.LogInformation("Redis configured (work queue).");
logger.LogInformation("Media blob storage configured.");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the media schema database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseTangleServicePipeline("Tangle Media API");
app.MapTangleServiceEndpoints();

app.Run();

public partial class Program { }
