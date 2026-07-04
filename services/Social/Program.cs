using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Social.Config;
using Social.Db;
using Social.Exceptions;
using Social.Infrastructure;
using Social.Security;
using Social.Telemetry;
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
        Title = "Tangle Social API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.Configure<InternalAccessOptions>(builder.Configuration.GetSection(InternalAccessOptions.SectionName));
builder.Services.AddScoped<InternalAccessAuthorizationFilter>();
builder.Services.AddSingleton<JwtBearerValidator>();

// Interim JWT validation: tokens are issued by the monolith (future users-service). Social only
// validates bearer tokens so [Authorize] and user-id reads work before a gateway exists.
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
builder.Services.AddTangleMonolithAccess(builder.Configuration);

builder.Services.AddDbContext<SocialDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres")
    .ForwardToPrometheus();

var app = builder.Build();

var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions);

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle Social API v1");
        options.RoutePrefix = "api";
    });

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
