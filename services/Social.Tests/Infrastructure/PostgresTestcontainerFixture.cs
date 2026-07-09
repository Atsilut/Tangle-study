using Social.Db;

namespace Social.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<SocialDbContext>(
        "tangle_social_test",
        options => new SocialDbContext(options));
