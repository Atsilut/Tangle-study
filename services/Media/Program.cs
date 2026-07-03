using Media.Global.Config;
using Media.Global.Db;
using Media.Global.Exceptions;
using Media.Global.Infrastructure;
using Media.Global.Security;
using Media.Global.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
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
        Title = "Tangle Media API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddYamlFile("media-config.yml", optional: false, reloadOnChange: true)
    // YAML is loaded after the host's default env vars; re-add so CD-injected Jwt__Secret wins.
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.AddSingleton<JwtBearerValidator>();

// Interim JWT validation: tokens are issued by the monolith (future users-service). Media only
// validates bearer tokens so [Authorize] and user-id reads work before a gateway exists.
// Target (MSA step 7): gateway validates once and forwards identity claims; slim or remove this.
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
            var jwtValidator = sp.GetRequiredService<JwtBearerValidator>();
            options.TokenValidationParameters = jwtValidator.GetValidationParameters();
            options.MapInboundClaims = false;
        }));

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleMedia(builder.Configuration);
builder.Services.AddTangleMonolithAccess(builder.Configuration);

builder.Services.AddDbContext<MediaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres");

var redisConfig = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
if (!string.IsNullOrWhiteSpace(redisConfig?.ConnectionString))
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
RedisStartupValidator.Validate(redisOptions);
if (!string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
    logger.LogInformation("Redis configured (work queue).");
else
    logger.LogInformation("Redis connection string empty; work queue uses no-op implementation.");

logger.LogInformation("Media blob storage configured.");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle Media API v1");
        options.RoutePrefix = "api";
    });

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
