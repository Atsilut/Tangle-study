using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Tangle.TestSupport.Fixtures;

public abstract class PostgresTestcontainerFixture<TDbContext>(
    string databaseName,
    Func<DbContextOptions<TDbContext>, TDbContext> createContext) : IAsyncLifetime
    where TDbContext : DbContext
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase(databaseName)
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    private readonly Func<DbContextOptions<TDbContext>, TDbContext> _createContext = createContext;

    public string ConnectionString => AppendTestPoolSettings(_postgres.GetConnectionString());

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<TDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = _createContext(options);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    public static string AppendTestPoolSettings(string connectionString)
    {
        if (connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        return connectionString.TrimEnd(';') + ";Maximum Pool Size=20;";
    }
}
