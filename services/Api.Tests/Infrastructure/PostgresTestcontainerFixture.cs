using Testcontainers.PostgreSql;

namespace Api.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgresTestcontainerFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithDatabase("tangle_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
    }

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
