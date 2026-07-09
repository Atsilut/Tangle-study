using Community.Db;

namespace Community.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<CommunityDbContext>(
        "tangle_community_test",
        options => new CommunityDbContext(options));
