using Community.Client;
using Community.Db;
using Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Community.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<CommunityWebApplicationFactory, Program>
{
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected InMemoryGroupClient GroupAccess => Factory.GroupAccess;
    protected FakeMediaClient FakeMedia => Factory.FakeMediaClient;
    protected FakeLocationClient FakeLocation => Factory.FakeLocationClient;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
        : base(
            () => new CommunityWebApplicationFactory(postgres.ConnectionString),
            factory => _ = factory.Services.GetRequiredService<IUserClient>())
    {
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllCommunityDataAsync();
        InMemoryUser.Reset();
        GroupAccess.Reset();
        FakeMedia.Reset();
        FakeLocation.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }

    protected async Task<Post?> FindPostEntityAsync(long postId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
        return await db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId);
    }

    protected async Task<Comment?> FindCommentEntityAsync(long commentId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
        return await db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId);
    }
}
