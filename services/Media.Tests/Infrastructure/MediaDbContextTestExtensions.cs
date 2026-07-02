using Media.Global.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Media.Tests.Infrastructure;

internal static class MediaDbContextTestExtensions
{
    public static async Task ClearAllMediaAssetsAsync(this MediaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.MediaAssets.ExecuteDeleteAsync(ct);
        db.ChangeTracker.Clear();
    }
}
