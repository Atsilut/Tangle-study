using Chat.Config;
using Chat.Db;
using Chat.Infrastructure;
using Chat.Realtime;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Tangle.AspNetCore.Config;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;
using Tangle.AspNetCore.OpenApi;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync<ChatDbContext>(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleServiceDefaults("Tangle Chat API");

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("chat-config.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<ChatMessagePolicyOptions>(
    builder.Configuration.GetSection(ChatMessagePolicyOptions.SectionName));
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleUsersAccess(builder.Configuration);
builder.Services.AddTangleSocialClient(builder.Configuration);
builder.Services.AddTangleGroupClient(builder.Configuration);
builder.Services.AddTangleMediaClient(builder.Configuration);

builder.Services.AddDbContext<ChatDbContext>(options =>
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

logger.LogInformation("Redis configured (SignalR backplane, work queue, pub/sub).");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the chat schema database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseTangleServicePipeline("Tangle Chat API");
app.MapTangleServiceEndpoints();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program { }
