using Media.Db;
using Tangle.TestSupport;

namespace Media.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<MediaDbContext>(
        "tangle_media_test",
        options => new MediaDbContext(options));
