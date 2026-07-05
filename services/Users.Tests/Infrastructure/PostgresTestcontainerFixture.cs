using Tangle.TestSupport;
using Users.Db;

namespace Users.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<UsersDbContext>(
        "tangle_test",
        options => new UsersDbContext(options));
