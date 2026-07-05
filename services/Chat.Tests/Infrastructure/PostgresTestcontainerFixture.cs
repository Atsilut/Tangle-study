using Chat.Db;
using Tangle.TestSupport;

namespace Chat.Tests.Infrastructure;

public sealed class PostgresTestcontainerFixture()
    : PostgresTestcontainerFixture<ChatDbContext>(
        "tangle_chat_test",
        options => new ChatDbContext(options));
