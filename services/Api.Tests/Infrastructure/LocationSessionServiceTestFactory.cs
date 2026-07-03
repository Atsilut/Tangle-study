using Api.Client;
using Api.Domain.Comments.Service;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Config;
using Api.Domain.Location.Realtime;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Service;
using Api.Domain.Location.Storage;
using Api.Domain.Posts.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Infrastructure;

internal static class LocationSessionServiceTestFactory
{
    internal sealed record Graph(
        LocationSessionService LocationSessionService,
        LocationSafetyAlertService SafetyAlertService,
        LiveLocationRedisStore LiveStore,
        FakeGroupMemberRepository GroupMemberRepository,
        FakeUserRepository UserRepository,
        FakeUserBlockRepository UserBlockRepository,
        UserBlockService UserBlockService,
        FakeLocationRealtimeNotifier RealtimeNotifier);

    internal sealed class FakeLocationRealtimeNotifier : ILocationRealtimeNotifier
    {
        public Domain.Location.Dto.LiveLocationGetResponseDto? LastNotification { get; private set; }
        public Domain.Location.Dto.LocationSessionEndedDto? LastSessionEnded { get; private set; }
        public Domain.Location.Dto.LocationSafetyAlertDto? LastSafetyAlert { get; set; }
        public IReadOnlyList<long> LastSafetyAlertRecipients { get; private set; } = [];
        public int SafetyAlertCount { get; private set; }

        public Task NotifyLocationUpdatedAsync(long sessionId, Domain.Location.Dto.LiveLocationGetResponseDto location)
        {
            LastNotification = location;
            return Task.CompletedTask;
        }

        public Task NotifyLocationSessionEndedAsync(long sessionId, Domain.Location.Dto.LocationSessionEndedDto ended)
        {
            LastSessionEnded = ended;
            return Task.CompletedTask;
        }

        public Task NotifySafetyAlertAsync(Domain.Location.Dto.LocationSafetyAlertDto alert, IReadOnlyList<long> recipientUserIds)
        {
            LastSafetyAlert = alert;
            LastSafetyAlertRecipients = recipientUserIds;
            SafetyAlertCount++;
            return Task.CompletedTask;
        }
    }

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var groupMemberRepository = new FakeGroupMemberRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var sessionRepository = new LocationSessionRepository(db);
        var distributedCache = new FakeDistributedCache();
        var realtimeNotifier = new FakeLocationRealtimeNotifier();
        var redisOptions = Options.Create(new RedisOptions { InstanceName = "tangle:" });
        var liveStore = new LiveLocationRedisStore(
            distributedCache,
            new ServiceCollection().BuildServiceProvider(),
            redisOptions,
            Options.Create(new LocationSafetyOptions()));
        var nicknameCacheService = DomainServiceTestFactory.CreateNicknameCacheService(userRepository, distributedCache);

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => null!),
            new Lazy<CommentService>(() => null!),
            new FakeMediaClient(),
            new FakeChatClient(),
            new Lazy<GroupMembershipService>(() => null!),
            new Lazy<MapPinService>(() => null!),
            new Lazy<LocationSessionService>(() => null!),
            http,
            nicknameCacheService,
            new NoOpEventPublisher());

        var userBlockRepository = new FakeUserBlockRepository();
        var userBlockService = new UserBlockService(
            userBlockRepository,
            new Lazy<FriendRequestService>(() => null!),
            userService,
            http);

        GroupService groupService = null!;
        var groupMembershipService = new GroupMembershipService(
            groupMemberRepository,
            new Lazy<GroupService>(() => groupService),
            userService,
            http);

        groupService = new GroupService(
            new FakeGroupRepository(),
            groupMembershipService,
            userService,
            db,
            http,
            new Lazy<GroupInvitationService>(() => null!),
            new Lazy<GroupApplicationService>(() => null!),
            new Lazy<GroupBlacklistService>(() => null!),
            new Lazy<GroupBoardService>(() => null!),
            new Lazy<PostService>(() => null!));

        var safetyAlertService = new LocationSafetyAlertService(
            sessionRepository,
            liveStore,
            userService,
            groupMembershipService,
            userBlockService,
            realtimeNotifier,
            distributedCache,
            Options.Create(new LocationSafetyOptions()),
            http);

        var locationSessionService = new LocationSessionService(
            sessionRepository,
            groupMembershipService,
            userBlockService,
            userService,
            liveStore,
            realtimeNotifier,
            safetyAlertService,
            http);

        return new Graph(
            locationSessionService,
            safetyAlertService,
            liveStore,
            groupMemberRepository,
            userRepository,
            userBlockRepository,
            userBlockService,
            realtimeNotifier);
    }

    public static async Task<long> SeedGroupWithMembersAsync(
        FakeGroupMemberRepository repo,
        long ownerId,
        params long[] memberIds)
    {
        const long groupId = 1;
        await repo.AddMemberAsync(new GroupMember(groupId, ownerId, GroupRole.Owner));
        foreach (var memberId in memberIds)
            await repo.AddMemberAsync(new GroupMember(groupId, memberId, GroupRole.Member));
        return groupId;
    }
}
