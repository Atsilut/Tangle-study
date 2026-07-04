using Microsoft.EntityFrameworkCore;
using Social.Db;
using Testcontainers.PostgreSql;

namespace Social.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgresTestcontainerFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase("tangle_social_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public string ConnectionString => AppendTestPoolSettings(_postgres.GetConnectionString());

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new SocialDbContext(options);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    internal static string AppendTestPoolSettings(string connectionString)
    {
        if (connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        return connectionString.TrimEnd(';') + ";Maximum Pool Size=20;";
    }
}
