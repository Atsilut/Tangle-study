using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Global.Db;

/// <summary>
/// Applies EF Core migrations and exits. Invoked via <c>dotnet Api.dll --migrate</c>.
/// </summary>
public static class DatabaseMigrationRunner
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("ConnectionStrings:DefaultConnection is not configured.");
            return 1;
        }

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        logger.LogInformation("Applying EF Core migrations...");
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("EF Core migrations applied successfully.");
        return 0;
    }
}
