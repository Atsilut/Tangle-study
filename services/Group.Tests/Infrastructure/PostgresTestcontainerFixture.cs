using Group.Db;

namespace Group.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<GroupDbContext>(
        "tangle_group_test",
        options => new GroupDbContext(options));
