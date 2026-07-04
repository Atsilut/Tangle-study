using Location.Db;
using Location.Repository;
using Location.Service;
using Microsoft.EntityFrameworkCore;

namespace Location.Tests.Infrastructure;

internal static class LocationServiceTestFactory
{
    internal sealed record Graph(
        MapPinService MapPinService,
        LocationClusterService LocationClusterService,
        LocationAccessService LocationAccessService,
        InMemoryMonolithAccessClient MonolithAccess,
        IMapPinRepository MapPinRepository);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var monolith = new InMemoryMonolithAccessClient(http);
        var db = new LocationDbContext(new DbContextOptionsBuilder<LocationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var mapPinRepository = new MapPinRepository(db);
        var distributedCache = new FakeDistributedCache();
        var workQueue = new FakeWorkQueue();

        var locationAccessService = new LocationAccessService(monolith, http);
        var locationClusterService = new LocationClusterService(
            mapPinRepository,
            distributedCache,
            workQueue);
        var mapPinService = new MapPinService(
            mapPinRepository,
            monolith,
            locationAccessService,
            new Lazy<LocationClusterService>(() => locationClusterService),
            http);

        return new Graph(
            mapPinService,
            locationClusterService,
            locationAccessService,
            monolith,
            mapPinRepository);
    }
}
