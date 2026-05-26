using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupTestScenario
{
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public GroupService GroupService { get; }
    public GroupMembershipService MembershipService { get; }
    public GroupJoinService JoinService { get; }
    public GroupApplicationService ApplicationService { get; }
    public GroupInvitationService InvitationService { get; }
    public GroupJoinResolutionService JoinResolution { get; }
    public GroupBlacklistService BlacklistService { get; }
    public GroupBoardAccessService BoardAccess { get; }
    public GroupBoardService BoardService { get; }
    public UserBlockService UserBlockService { get; }
    public FakeGroupRepository GroupRepository { get; }
    public FakeGroupMemberRepository GroupMemberRepository { get; }
    public FakeGroupInvitationRepository InvitationRepository { get; }
    public FakeGroupApplicationRepository ApplicationRepository { get; }
    public FakeGroupBlacklistRepository BlacklistRepository { get; }
    public FakeGroupBoardRepository BoardRepository { get; }
    private readonly FakeUserRepository _userRepository;

    public User Owner { get; private set; } = null!;
    public User Admin { get; private set; } = null!;
    public User AdminB { get; private set; } = null!;
    public User Member { get; private set; } = null!;
    public User MemberB { get; private set; } = null!;
    public User Stranger { get; private set; } = null!;

    private GroupTestScenario(FakeHttpContextAccessor httpContextAccessor, DomainServiceTestFactory.Graph graph)
    {
        _httpContextAccessor = httpContextAccessor;
        GroupService = graph.GroupService;
        MembershipService = graph.GroupMembershipService;
        JoinService = graph.GroupJoinService;
        ApplicationService = graph.GroupApplicationService;
        InvitationService = graph.GroupInvitationService;
        JoinResolution = graph.GroupJoinResolutionService;
        BlacklistService = graph.GroupBlacklistService;
        BoardAccess = graph.GroupBoardAccessService;
        BoardService = graph.GroupBoardService;
        UserBlockService = graph.UserBlockService;
        GroupRepository = graph.GroupRepository;
        GroupMemberRepository = graph.GroupMemberRepository;
        InvitationRepository = graph.GroupInvitationRepository;
        ApplicationRepository = graph.GroupApplicationRepository;
        BlacklistRepository = graph.GroupBlacklistRepository;
        BoardRepository = graph.GroupBoardRepository;
        _userRepository = graph.UserRepository;
    }

    public static async Task<GroupTestScenario> CreateAsync(string nicknamePrefix)
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var scenario = new GroupTestScenario(http, graph);
        scenario.Owner = await scenario.CreateTestUserAsync($"{nicknamePrefix}_owner");
        scenario.Admin = await scenario.CreateTestUserAsync($"{nicknamePrefix}_admin");
        scenario.AdminB = await scenario.CreateTestUserAsync($"{nicknamePrefix}_adminB");
        scenario.Member = await scenario.CreateTestUserAsync($"{nicknamePrefix}_member");
        scenario.MemberB = await scenario.CreateTestUserAsync($"{nicknamePrefix}_memberB");
        scenario.Stranger = await scenario.CreateTestUserAsync($"{nicknamePrefix}_stranger");
        return scenario;
    }

    private async Task<User> CreateTestUserAsync(string nickname)
    {
        var user = new User(
            email: $"{nickname}@test.com",
            password: "password",
            nickname: nickname);
        await _userRepository.CreateUserAsync(user);
        return user;
    }

    public void LoginAs(GroupActorRole role)
    {
        if (role == GroupActorRole.Anonymous)
        {
            LoginAnonymous();
            return;
        }

        LoginAs(ResolveActorUserId(role));
    }

    public void LoginAs(long userId) =>
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    public void LoginAnonymous() => _httpContextAccessor.HttpContext = new DefaultHttpContext();

    public long ResolveActorUserId(GroupActorRole role) => role switch
    {
        GroupActorRole.Owner => Owner.Id,
        GroupActorRole.Admin => Admin.Id,
        GroupActorRole.Member => Member.Id,
        GroupActorRole.Stranger => Stranger.Id,
        GroupActorRole.Anonymous => throw new InvalidOperationException("Anonymous has no user id."),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public long ResolveTargetUserId(GroupTargetRole target, GroupActorRole caller) => target switch
    {
        GroupTargetRole.Owner => Owner.Id,
        GroupTargetRole.Admin => Admin.Id,
        GroupTargetRole.OtherAdmin => AdminB.Id,
        GroupTargetRole.Member => Member.Id,
        GroupTargetRole.OtherMember => MemberB.Id,
        GroupTargetRole.Self => ResolveActorUserId(caller),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    public async Task<GroupResponseDto> SetupGroupAsync(
        GroupVisibility visibility,
        bool includeAdmin = true,
        bool includeMember = true,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable)
    {
        LoginAs(Owner.Id);
        var group = await GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "TestGroup",
            Description = "test",
            Visibility = visibility,
            JoinPolicy = joinPolicy,
        });
        if (includeAdmin)
        {
            await MembershipService.AddMemberInternalAsync(group.Id, Admin.Id, GroupRole.Admin);
            await MembershipService.AddMemberInternalAsync(group.Id, AdminB.Id, GroupRole.Admin);
        }
        if (includeMember)
        {
            await MembershipService.AddMemberInternalAsync(group.Id, Member.Id, GroupRole.Member);
            await MembershipService.AddMemberInternalAsync(group.Id, MemberB.Id, GroupRole.Member);
        }
        return group;
    }

    public async Task<GroupBoard> SeedBoardAsync(long groupId, string name, BoardVisibility visibility)
    {
        var board = new GroupBoard(groupId, name, visibility);
        await BoardRepository.CreateAsync(board);
        return board;
    }

    public async Task<GroupInvitation> SeedInvitationAsync(
        long groupId,
        long inviterId,
        long inviteeId,
        bool isPending = true)
    {
        var invitation = new GroupInvitation(groupId, inviterId, inviteeId);
        if (!isPending)
            invitation.Ignore();
        await InvitationRepository.CreateInvitationAsync(invitation);
        return invitation;
    }

    public async Task<GroupApplication> SeedApplicationAsync(
        long groupId,
        long applicantId,
        bool isPending = true)
    {
        var application = new GroupApplication(groupId, applicantId);
        if (!isPending)
            application.Ignore();
        await ApplicationRepository.CreateApplicationAsync(application);
        return application;
    }

    public async Task BlacklistUserAsync(long groupId, long userId)
    {
        LoginAs(Owner.Id);
        await BlacklistService.AddAsync(groupId, new GroupBlacklistCreateRequestDto { UserId = userId });
    }

    public async Task<GroupResponseDto> SetupInvitationOnlyGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
        await SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.InvitationOnly);

    public async Task<GroupResponseDto> SetupRequestableGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
        await SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.Requestable);

    public async Task<GroupInvitation> InviteStrangerAsync(long groupId, GroupActorRole inviter = GroupActorRole.Owner)
    {
        LoginAs(inviter);
        var result = await InvitationService.InviteAsync(
            groupId,
            new GroupInvitationCreateRequestDto { InviteeId = Stranger.Id });
        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        return (await InvitationRepository.GetForUserAsync(groupId, Stranger.Id))!;
    }

    public async Task<GroupApplication> ApplyAsStrangerAsync(long groupId)
    {
        LoginAs(Stranger.Id);
        var result = await ApplicationService.ApplyAsync(groupId);
        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        return (await ApplicationRepository.GetForUserAsync(groupId, Stranger.Id))!;
    }

    public async Task AssertMemberRoleAsync(long groupId, long userId, GroupRole expected)
    {
        var member = await GroupMemberRepository.GetMemberAsync(groupId, userId);
        Assert.NotNull(member);
        Assert.Equal(expected, member!.Role);
    }

    public async Task AssertMemberAbsentAsync(long groupId, long userId) =>
        Assert.Null(await GroupMemberRepository.GetMemberAsync(groupId, userId));

    public async Task AssertIsMemberAsync(long groupId, long userId, bool expected) =>
        Assert.Equal(expected, await MembershipService.IsMemberAsync(groupId, userId));
}

public enum GroupActorRole
{
    Anonymous,
    Owner,
    Admin,
    Member,
    Stranger,
}

public enum GroupTargetRole
{
    Owner,
    Admin,
    OtherAdmin,
    Member,
    OtherMember,
    Self,
}

public enum GroupReadOperation
{
    GetGroup,
    GetMembers,
}

public enum GroupManagementAction
{
    Update,
    Delete,
    TransferToMember,
    TransferToSelf,
    TransferToStranger,
}

public enum GroupExpectedOutcome
{
    Ok,
    NotFound,
    Unauthorized,
    ArgumentException,
}

public enum JoinPolicyOperation
{
    Join,
    Apply,
}

public enum JoinPolicyRouteOutcome
{
    MemberAdded,
    UseJoinEndpoint,
    RequiresApplication,
    InvitationOnly,
    ApplicationCreated,
}

public enum BlacklistAdminAction
{
    Add,
    Remove,
}

public enum InvitationRequestAction
{
    Accept,
    AcceptAsNonInvitee,
    Reject,
    Ignore,
    Cancel,
    CancelAsNonInviterAdmin,
}

public enum ApplicationRequestAction
{
    Approve,
    ApproveAsApplicant,
    Reject,
    Ignore,
    Cancel,
}

public enum BoardCrudOperation
{
    Create,
    Update,
    Delete,
}
