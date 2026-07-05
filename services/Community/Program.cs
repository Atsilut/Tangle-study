using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Community.Config;
using Community.Db;
using Community.Exceptions;
using Community.Infrastructure;
using Community.Security;
using Community.Telemetry;
using Prometheus;

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
        Title = "Tangle Community API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddEnvironmentVariables();

builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.Configure<InternalAccessOptions>(builder.Configuration.GetSection(InternalAccessOptions.SectionName));
builder.Services.Configure<GatewayIdentityOptions>(builder.Configuration.GetSection(GatewayIdentityOptions.SectionName));
builder.Services.AddScoped<InternalAccessAuthorizationFilter>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = GatewayIdentityAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = GatewayIdentityAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, GatewayIdentityAuthenticationHandler>(
        GatewayIdentityAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
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
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle Community API v1");
        options.RoutePrefix = "api";
    });

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

app.UseRouting();
app.UseHttpMetrics();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MetricsScrapeAuthMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

app.Run();
