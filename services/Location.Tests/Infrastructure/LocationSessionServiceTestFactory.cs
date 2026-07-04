using Location.Config;
using Location.Db;
using Location.Dto;
using Location.Realtime;
using Location.Repository;
using Location.Service;
using Location.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Location.Tests.Infrastructure;

internal static class LocationSessionServiceTestFactory
{
    internal sealed record Graph(
        LocationSessionService LocationSessionService,
        LocationSafetyAlertService SafetyAlertService,
        LiveLocationRedisStore LiveStore,
        InMemoryMonolithAccessClient MonolithAccess,
        FakeLocationRealtimeNotifier RealtimeNotifier);

    internal sealed class FakeLocationRealtimeNotifier : ILocationRealtimeNotifier
    {
        public LiveLocationGetResponseDto? LastNotification { get; private set; }
        public LocationSessionEndedDto? LastSessionEnded { get; private set; }
        public LocationSafetyAlertDto? LastSafetyAlert { get; set; }
        public IReadOnlyList<long> LastSafetyAlertRecipients { get; private set; } = [];
        public int SafetyAlertCount { get; private set; }

        public Task NotifyLocationUpdatedAsync(long sessionId, LiveLocationGetResponseDto location)
        {
            LastNotification = location;
            return Task.CompletedTask;
        }

        public Task NotifyLocationSessionEndedAsync(long sessionId, LocationSessionEndedDto ended)
        {
            LastSessionEnded = ended;
            return Task.CompletedTask;
        }

        public Task NotifySafetyAlertAsync(LocationSafetyAlertDto alert, IReadOnlyList<long> recipientUserIds)
        {
            LastSafetyAlert = alert;
            LastSafetyAlertRecipients = recipientUserIds;
            SafetyAlertCount++;
            return Task.CompletedTask;
        }
    }

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var monolith = new InMemoryMonolithAccessClient(http);
        var db = new LocationDbContext(new DbContextOptionsBuilder<LocationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var sessionRepository = new LocationSessionRepository(db);
        var distributedCache = new FakeDistributedCache();
        var realtimeNotifier = new FakeLocationRealtimeNotifier();
        var redisOptions = Options.Create(LocationTestOptions.Redis);
        var liveStore = new LiveLocationRedisStore(
            distributedCache,
            new ServiceCollection().BuildServiceProvider(),
            redisOptions,
            Options.Create(LocationTestOptions.Safety));

        var safetyAlertService = new LocationSafetyAlertService(
            sessionRepository,
            liveStore,
            monolith,
            realtimeNotifier,
            distributedCache,
            Options.Create(LocationTestOptions.Safety),
            http);

        var locationSessionService = new LocationSessionService(
            sessionRepository,
            monolith,
            liveStore,
            realtimeNotifier,
            safetyAlertService,
            http);

        return new Graph(
            locationSessionService,
            safetyAlertService,
            liveStore,
            monolith,
            realtimeNotifier);
    }

    public static long SeedGroupWithMembers(InMemoryMonolithAccessClient monolith, long ownerId, params long[] memberIds)
    {
        const long groupId = 1;
        monolith.AddGroupMember(groupId, ownerId);
        foreach (var memberId in memberIds)
            monolith.AddGroupMember(groupId, memberId);
        return groupId;
    }
}
