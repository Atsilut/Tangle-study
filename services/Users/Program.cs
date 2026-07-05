using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Users.Config;
using Users.Db;
using Users.Exceptions;
using Users.Infrastructure;
using Users.Security;
using Users.Telemetry;
using Prometheus;
using StackExchange.Redis;

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
        Title = "Tangle Users API",
    });
});

builder.Services.AddCustomDependencies();

builder.Configuration
    .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<InternalAccessOptions>(builder.Configuration.GetSection(InternalAccessOptions.SectionName));
builder.Services.Configure<GatewayIdentityOptions>(builder.Configuration.GetSection(GatewayIdentityOptions.SectionName));
builder.Services.AddScoped<InternalAccessAuthorizationFilter>();
builder.Services.AddSingleton<TokenProvider>();

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
builder.Services.AddTangleRedis(builder.Configuration);
builder.Services.AddTangleMediaClient(builder.Configuration);
builder.Services.AddTangleChatClient(builder.Configuration);
builder.Services.AddTangleLocationClient(builder.Configuration);
builder.Services.AddTangleCommunityClient(builder.Configuration);
builder.Services.AddTangleGroupClient(builder.Configuration);
builder.Services.AddTangleSocialClient(builder.Configuration);

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(defaultConnection, name: "postgres");

var redisOptions = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
RedisStartupValidator.Validate(redisOptions);
healthChecksBuilder.AddRedis(
    sp => sp.GetRequiredService<IConnectionMultiplexer>(),
    name: "redis",
    timeout: TimeSpan.FromSeconds(5));

healthChecksBuilder.ForwardToPrometheus();

var app = builder.Build();

var jwtOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
JwtStartupValidator.Validate(app.Environment, jwtOptions);

var logger = app.Services.GetRequiredService<ILogger<Program>>();
DependencyInjection.PrintLogs(logger);

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tangle Users API v1");
        options.RoutePrefix = "api";
    });

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Database:ResetOnStartup"))
    {
        logger.LogWarning("Database:ResetOnStartup is enabled; dropping the users schema database.");
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
