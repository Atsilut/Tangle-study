using Location.Db;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Location.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgresTestcontainerFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase("tangle_location_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public string ConnectionString => AppendTestPoolSettings(_postgres.GetConnectionString());

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<LocationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new LocationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    internal static string AppendTestPoolSettings(string connectionString)
    {
        if (connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        return connectionString + ";Maximum Pool Size=30;Timeout=30";
    }
}
