using Chat.Db;
using Chat.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tangle.AspNetCore.Outbox;

namespace Chat.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<ChatWebApplicationFactory, Program>
{
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeMediaClient FakeMediaClient => Factory.FakeMediaClient;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
        : base(() => new ChatWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString))
    {
        Redis = redis;
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllChatDataAsync();
        InMemoryUser.Reset();
        FakeMediaClient.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }

    protected async Task<ChatMessage?> FindChatMessageEntityAsync(long messageId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        return await db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId);
    }

    protected async Task<IReadOnlyList<OutboxMessage>> GetOutboxMessagesByEntityIdAsync(long entityId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        return await db.OutboxMessages.AsNoTracking()
            .Where(m => m.EntityId == entityId)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }
}
