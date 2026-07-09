using Community.Config;
using Community.Db;
using Community.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync<CommunityDbContext>(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleServiceDefaults("Tangle Community API");

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddEnvironmentVariables();

builder.Services.AddTangleUsersAccess(builder.Configuration);
builder.Services.AddTangleSocialClient(builder.Configuration);
builder.Services.AddTangleGroupClient(builder.Configuration);
builder.Services.AddTangleMediaClient(builder.Configuration);
builder.Services.AddTangleLocationClient(builder.Configuration);

builder.Services.AddDbContext<CommunityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres")
    .ForwardToPrometheus();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the community schema database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseTangleServicePipeline("Tangle Community API");
app.MapTangleServiceEndpoints();

app.Run();

public partial class Program { }
