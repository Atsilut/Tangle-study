using Api.Global.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Infrastructure;

public static class AppDbContextTestExtensions
{
    public static async Task ClearAllEntitiesAsync(this ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.FriendRequests.ExecuteDeleteAsync(ct);
        await db.UserBlocks.ExecuteDeleteAsync(ct);
        await db.Friendships.ExecuteDeleteAsync(ct);
        await db.GroupMembers.ExecuteDeleteAsync(ct);
        await db.GroupApplications.ExecuteDeleteAsync(ct);
        await db.GroupInvitations.ExecuteDeleteAsync(ct);
        await db.GroupBlacklists.ExecuteDeleteAsync(ct);
        await db.MapPins.ExecuteDeleteAsync(ct);
        await db.LocationSessions.ExecuteDeleteAsync(ct);
        await db.Comments.ExecuteDeleteAsync(ct);
        await db.Posts.ExecuteDeleteAsync(ct);
        await db.GroupBoards.ExecuteDeleteAsync(ct);
        await db.Groups.ExecuteDeleteAsync(ct);
        await db.Users.ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();
    }
}
