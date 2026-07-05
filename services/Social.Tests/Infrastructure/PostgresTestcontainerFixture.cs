using Microsoft.EntityFrameworkCore;
using Social.Db;
using Tangle.TestSupport;

namespace Social.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<SocialDbContext>(
        "tangle_social_test",
        options => new SocialDbContext(options));
