using Chat.Config;
using Chat.Db;
using Chat.Exceptions;
using Chat.Infrastructure;
using Chat.Realtime;
using Chat.Security;
using Chat.Telemetry;
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
        Title = "Tangle Chat API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddYamlFile("chat-config.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<ChatMessagePolicyOptions>(
    builder.Configuration.GetSection(ChatMessagePolicyOptions.SectionName));
builder.Services.AddSingleton<JwtBearerValidator>();

// Interim JWT validation: tokens are issued by the monolith (future users-service). Chat only
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
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleMonolithAccess(builder.Configuration);
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

var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions);

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

logger.LogInformation("Redis configured (SignalR backplane, work queue, pub/sub).");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle Chat API v1");
        options.RoutePrefix = "api";
    });

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

app.UseRouting();
app.UseHttpMetrics();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MetricsScrapeAuthMiddleware>();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");
app.MapMetrics();

app.Run();
