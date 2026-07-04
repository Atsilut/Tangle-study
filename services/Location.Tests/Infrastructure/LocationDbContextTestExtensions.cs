using Location.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Location.Tests.Infrastructure;

internal static class LocationDbContextTestExtensions
{
    public static async Task ClearAllLocationDataAsync(this LocationWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LocationDbContext>();
        await db.LocationSessions.ExecuteDeleteAsync();
        await db.MapPins.ExecuteDeleteAsync();
    }
}
