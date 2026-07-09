using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using Users.Config;
using Users.Db;
using Users.Infrastructure;
using Users.Security;
using Tangle.AspNetCore.Db;
using Tangle.AspNetCore.Hosting;
using Tangle.AspNetCore.Security;

namespace Users
{
    public partial class UsersProgram
    {
        public static async Task Main(string[] args)
        {
            if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
            {
                var exitCode = await DatabaseMigrationRunner.RunAsync<UsersDbContext>(args);
                Environment.Exit(exitCode);
            }

            var builder = WebApplication.CreateBuilder(args);

            builder.AddTangleServiceDefaults("Tangle Users API");

            builder.Services.AddCustomDependencies();

            builder.Configuration
                .AddYamlFile("security.yml", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
            builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
            builder.Services.AddSingleton<TokenProvider>();
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
            JwtStartupValidator.Validate(app.Environment, jwtOptions.Secret);

            var logger = app.Services.GetRequiredService<ILogger<UsersProgram>>();
            DependencyInjection.PrintLogs(logger);

            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
            {
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

            app.UseTangleServicePipeline("Tangle Users API");
            app.MapTangleServiceEndpoints();

            app.Run();
        }
    }
}

public partial class Program { }
