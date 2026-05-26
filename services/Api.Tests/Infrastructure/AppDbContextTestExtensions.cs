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
        await db.FriendRequests.ExecuteDeleteAsync();
        await db.UserBlocks.ExecuteDeleteAsync();
        await db.Friendships.ExecuteDeleteAsync();
        await db.GroupMembers.ExecuteDeleteAsync();
        await db.GroupApplications.ExecuteDeleteAsync();
        await db.GroupInvitations.ExecuteDeleteAsync();
        await db.GroupBlacklists.ExecuteDeleteAsync();
        await db.GroupBoards.ExecuteDeleteAsync();
        await db.Comments.ExecuteDeleteAsync();
        await db.Posts.ExecuteDeleteAsync();
        await db.Groups.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }
}
