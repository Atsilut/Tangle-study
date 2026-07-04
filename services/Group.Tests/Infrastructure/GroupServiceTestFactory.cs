using Group.Db;
using Group.Service;
using Group.Tests.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Group.Tests.Infrastructure;

internal static class GroupServiceTestFactory
{
    internal sealed record Graph(
        GroupService GroupService,
        GroupMembershipService GroupMembershipService,
        GroupApplicationService GroupApplicationService,
        GroupInvitationService GroupInvitationService,
        GroupJoinResolutionService GroupJoinResolutionService,
        GroupJoinService GroupJoinService,
        GroupBlacklistService GroupBlacklistService,
        GroupBoardAccessService GroupBoardAccessService,
        GroupBoardService GroupBoardService,
        FakeGroupRepository GroupRepository,
        FakeGroupMemberRepository GroupMemberRepository,
        FakeGroupApplicationRepository GroupApplicationRepository,
        FakeGroupInvitationRepository GroupInvitationRepository,
        FakeGroupBlacklistRepository GroupBlacklistRepository,
        FakeGroupBoardRepository GroupBoardRepository,
        InMemoryMonolithAccessClient MonolithAccess,
        FakeCommunityClient CommunityClient,
        FakeLocationClient LocationClient,
        FakeHttpContextAccessor Http);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var groupRepository = new FakeGroupRepository();
        var groupMemberRepository = new FakeGroupMemberRepository();
        var groupApplicationRepository = new FakeGroupApplicationRepository();
        var groupInvitationRepository = new FakeGroupInvitationRepository();
        var groupBlacklistRepository = new FakeGroupBlacklistRepository();
        var groupBoardRepository = new FakeGroupBoardRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var monolith = new InMemoryMonolithAccessClient();
        var communityClient = new FakeCommunityClient();
        var locationClient = new FakeLocationClient();
        var db = new GroupDbContext(new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        GroupMembershipService groupMembershipService = null!;
        GroupJoinResolutionService groupJoinResolutionService = null!;
        GroupJoinService groupJoinService = null!;
        GroupApplicationService groupApplicationService = null!;
        GroupInvitationService groupInvitationService = null!;
        GroupBlacklistService groupBlacklistService = null!;
        GroupService groupService = null!;
        GroupBoardService groupBoardService = null!;

        groupMembershipService = new GroupMembershipService(
            groupMemberRepository,
            new Lazy<GroupService>(() => groupService),
            monolith,
            http);

        var groupBoardAccessService = new GroupBoardAccessService(
            groupBoardRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            http);

        groupBlacklistService = new GroupBlacklistService(
            groupBlacklistRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            monolith,
            db,
            http);

        groupApplicationService = new GroupApplicationService(
            groupApplicationRepository,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            monolith,
            db,
            http);

        groupInvitationService = new GroupInvitationService(
            groupInvitationRepository,
            new Lazy<GroupApplicationService>(() => groupApplicationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            monolith,
            db,
            http);

        groupJoinResolutionService = new GroupJoinResolutionService(
            groupMembershipService,
            groupInvitationService,
            groupApplicationService,
            groupBlacklistService,
            db);

        groupJoinService = new GroupJoinService(
            new Lazy<GroupService>(() => groupService),
            groupInvitationService,
            groupMembershipService,
            groupJoinResolutionService,
            groupBlacklistService,
            http);

        groupBoardService = new GroupBoardService(
            groupBoardRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            groupBoardAccessService,
            http);

        groupService = new GroupService(
            groupRepository,
            groupMembershipService,
            monolith,
            db,
            http,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupApplicationService>(() => groupApplicationService),
            new Lazy<GroupBlacklistService>(() => groupBlacklistService),
            new Lazy<GroupBoardService>(() => groupBoardService),
            communityClient,
            locationClient);

        return new Graph(
            groupService,
            groupMembershipService,
            groupApplicationService,
            groupInvitationService,
            groupJoinResolutionService,
            groupJoinService,
            groupBlacklistService,
            groupBoardAccessService,
            groupBoardService,
            groupRepository,
            groupMemberRepository,
            groupApplicationRepository,
            groupInvitationRepository,
            groupBlacklistRepository,
            groupBoardRepository,
            monolith,
            communityClient,
            locationClient,
            http);
    }
}
