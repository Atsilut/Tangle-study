using Microsoft.EntityFrameworkCore;
using Prometheus;
using Social.Config;
using Social.Db;
using Social.Infrastructure;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await DatabaseMigrationRunner.RunAsync<SocialDbContext>(args);
    Environment.Exit(exitCode);
}

var builder = WebApplication.CreateBuilder(args);

builder.AddTangleServiceDefaults("Tangle Social API");

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddEnvironmentVariables();

builder.Services.AddTangleUsersAccess(builder.Configuration);

builder.Services.AddDbContext<SocialDbContext>(options =>
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
    var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the social schema database.");
        db.Database.EnsureDeleted();
    }

    db.Database.Migrate();
}

app.UseTangleServicePipeline("Tangle Social API");
app.MapTangleServiceEndpoints();

app.Run();

public partial class Program { }
