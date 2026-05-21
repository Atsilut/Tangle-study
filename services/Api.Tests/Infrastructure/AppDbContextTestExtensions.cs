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
        await db.Friendships.ExecuteDeleteAsync();
        await db.Comments.ExecuteDeleteAsync();
        await db.Posts.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }
}
