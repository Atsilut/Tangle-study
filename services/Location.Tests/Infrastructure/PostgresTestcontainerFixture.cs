using Location.Db;

namespace Location.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<LocationDbContext>(
        "tangle_location_test",
        options => new LocationDbContext(options));
