using Location.Db;
using Location.Repository;
using Location.Service;
using Microsoft.EntityFrameworkCore;
using Tangle.AspNetCore.Auth;

namespace Location.Tests.Infrastructure;

internal static class LocationServiceTestFactory
{
    internal sealed record Graph(
        MapPinService MapPinService,
        LocationClusterService LocationClusterService,
        LocationAccessService LocationAccessService,
        InMemoryUserClient InMemoryUser,
        FakeSocialClient FakeSocial,
        FakeCommunityAccessClient FakeCommunity,
        FakeGroupClient FakeGroup,
        IMapPinRepository MapPinRepository);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var currentUser = new CurrentUserAccessor(http);
        var users = new InMemoryUserClient();
        var social = new FakeSocialClient();
        var community = new FakeCommunityAccessClient(http);
        var group = new FakeGroupClient(users);
        var db = new LocationDbContext(new DbContextOptionsBuilder<LocationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var mapPinRepository = new MapPinRepository(db);
        var distributedCache = new FakeDistributedCache();
        var workQueue = new FakeWorkQueue();

        var locationAccessService = new LocationAccessService(users, social, community, currentUser);
        var locationClusterService = new LocationClusterService(
            mapPinRepository,
            distributedCache,
            workQueue);
        var mapPinService = new MapPinService(
            mapPinRepository,
            users,
            locationAccessService,
            new Lazy<LocationClusterService>(() => locationClusterService),
            currentUser);

        return new Graph(
            mapPinService,
            locationClusterService,
            locationAccessService,
            users,
            social,
            community,
            group,
            mapPinRepository);
    }
}
