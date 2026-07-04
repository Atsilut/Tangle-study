using Community.Client;
using Community.Db;
using Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Community.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected CommunityWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected InMemoryMonolithAccessClient MonolithAccess => Factory.MonolithAccess;
    protected FakeMediaClient FakeMedia => Factory.FakeMediaClient;
    protected FakeLocationClient FakeLocation => Factory.FakeLocationClient;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
    {
        Factory = new CommunityWebApplicationFactory(postgres.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IMonolithAccessClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllCommunityDataAsync();
        MonolithAccess.Reset();
        FakeMedia.Reset();
        FakeLocation.Reset();
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
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
