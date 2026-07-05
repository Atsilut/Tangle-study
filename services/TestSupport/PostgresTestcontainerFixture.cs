using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Tangle.TestSupport;

public abstract class PostgresTestcontainerFixture<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    private readonly PostgreSqlContainer _postgres;
    private readonly string _databaseName;
    private readonly Func<DbContextOptions<TDbContext>, TDbContext> _createContext;

    protected PostgresTestcontainerFixture(
        string databaseName,
        Func<DbContextOptions<TDbContext>, TDbContext> createContext)
    {
        _databaseName = databaseName;
        _createContext = createContext;
        _postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase(databaseName)
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

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
