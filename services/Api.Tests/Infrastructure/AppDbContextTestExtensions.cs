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
        await db.Users.ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();
    }
}
