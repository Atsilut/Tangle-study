using Group.Db;
using Group.Service;
using Group.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Tangle.AspNetCore.Auth;

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
        GroupBoardService GroupBoardService,
        FakeGroupRepository GroupRepository,
        FakeGroupMemberRepository GroupMemberRepository,
        FakeGroupApplicationRepository GroupApplicationRepository,
        FakeGroupInvitationRepository GroupInvitationRepository,
        FakeGroupBlacklistRepository GroupBlacklistRepository,
        FakeGroupBoardRepository GroupBoardRepository,
        InMemoryUserClient InMemoryUser,
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
        var currentUser = new CurrentUserAccessor(http);
        var monolith = new InMemoryUserClient();
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
            currentUser);

        groupBoardService = new GroupBoardService(
            groupBoardRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            currentUser);

        groupBlacklistService = new GroupBlacklistService(
            groupBlacklistRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            monolith,
            db,
            currentUser);

        groupApplicationService = new GroupApplicationService(
            groupApplicationRepository,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            monolith,
            db,
            currentUser);

        groupInvitationService = new GroupInvitationService(
            groupInvitationRepository,
            new Lazy<GroupApplicationService>(() => groupApplicationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            monolith,
            monolith,
            db,
            currentUser);

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
            currentUser);

        groupService = new GroupService(
            groupRepository,
            groupMembershipService,
            monolith,
            db,
            currentUser,
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
