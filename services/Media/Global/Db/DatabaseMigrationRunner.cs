using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.RegularExpressions;

namespace Media.Global.Db;

/// <summary>
/// Applies EF Core migrations and exits. Invoked via <c>dotnet Media.dll --migrate</c>.
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

        if (!TryValidateConnectionString(connectionString, out var validationMessage))
        {
            Console.Error.WriteLine(validationMessage);
            return 1;
        }

        builder.Services.AddDbContext<MediaDbContext>(options =>
            options.UseNpgsql(connectionString));

        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        logger.LogInformation("Applying EF Core migrations...");
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (ArgumentException ex) when (IsConnectionStringParseFailure(ex))
        {
            Console.Error.WriteLine(BuildMalformedConnectionMessage());
            logger.LogError("Database migration failed due to a malformed connection string.");
            return 1;
        }
        catch (Exception ex) when (ex.InnerException is ArgumentException inner && IsConnectionStringParseFailure(inner))
        {
            Console.Error.WriteLine(BuildMalformedConnectionMessage());
            logger.LogError("Database migration failed due to a malformed connection string.");
            return 1;
        }
        catch (Exception)
        {
            logger.LogError("Database migration failed.");
            return 1;
        }

        logger.LogInformation("EF Core migrations applied successfully.");
        return 0;
    }

    private static bool TryValidateConnectionString(string connectionString, out string message)
    {
        if (Regex.IsMatch(connectionString, @"\?sslmode(?:$|[&#])", RegexOptions.IgnoreCase))
        {
            message = BuildTruncatedSslModeMessage();
            return false;
        }

        if (Regex.IsMatch(connectionString, @"(?:^|[?&])sslmode=(?:$|[&#])", RegexOptions.IgnoreCase))
        {
            message = BuildTruncatedSslModeMessage();
            return false;
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            message = string.Empty;
            return true;
        }
        catch (ArgumentException)
        {
            message = BuildMalformedConnectionMessage();
            return false;
        }
    }

    private static bool IsConnectionStringParseFailure(ArgumentException ex) =>
        ex.Message.Contains("Couldn't set", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase);

    private static string BuildTruncatedSslModeMessage() =>
        "Connection string appears truncated at '?sslmode'. "
        + "Use ?sslmode=require|verify-ca|verify-full or Npgsql format with "
        + "SSL Mode=Require|VerifyCA|VerifyFull.";

    private static string BuildMalformedConnectionMessage() =>
        "Connection string appears malformed."
            + " Use Npgsql format (Host=...;Database=...;Username=...;Password=...;"
            + " SSL Mode=Require|VerifyCA|VerifyFull)"
            + " or postgresql://user:pass@host/db?sslmode=require|verify-ca|verify-full."
            + " For Neon, include a full sslmode value (require, verify-ca, or verify-full).";
}
