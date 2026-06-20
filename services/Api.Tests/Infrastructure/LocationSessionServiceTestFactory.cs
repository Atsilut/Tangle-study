using Api.Domain.Chat.Service;
using Api.Domain.Comments.Service;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Realtime;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Service;
using Api.Domain.Location.Storage;
using Api.Domain.Media.Service;
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
        FakeGroupMemberRepository GroupMemberRepository,
        FakeUserRepository UserRepository,
        FakeLocationRealtimeNotifier RealtimeNotifier);

    internal sealed class FakeLocationRealtimeNotifier : ILocationRealtimeNotifier
    {
        public Domain.Location.Dto.LiveLocationGetResponseDto? LastNotification { get; private set; }

        public Task NotifyLocationUpdatedAsync(long sessionId, Domain.Location.Dto.LiveLocationGetResponseDto location)
        {
            LastNotification = location;
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
            redisOptions);
        var nicknameCacheService = DomainServiceTestFactory.CreateNicknameCacheService(userRepository, distributedCache);

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => null!),
            new Lazy<CommentService>(() => null!),
            new Lazy<MediaService>(() => null!),
            new Lazy<ChatMessageService>(() => null!),
            new Lazy<ChatRoomService>(() => null!),
            new Lazy<GroupMembershipService>(() => null!),
            new Lazy<MapPinService>(() => null!),
            new Lazy<LocationSessionService>(() => null!),
            http,
            nicknameCacheService,
            new NoOpEventPublisher());

        var userBlockService = new UserBlockService(
            new FakeUserBlockRepository(),
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

        var locationSessionService = new LocationSessionService(
            sessionRepository,
            groupMembershipService,
            userBlockService,
            userService,
            liveStore,
            realtimeNotifier,
            http);

        return new Graph(
            locationSessionService,
            groupMemberRepository,
            userRepository,
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
