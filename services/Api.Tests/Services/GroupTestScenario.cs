using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.Users.Domain;
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
    public GroupBoardAccessService BoardAccess { get; }
    public GroupBoardService BoardService { get; }
    public FakeGroupRepository GroupRepository { get; }
    public FakeGroupMemberRepository GroupMemberRepository { get; }
    public FakeGroupBoardRepository BoardRepository { get; }
    private readonly FakeUserRepository _userRepository;

    public User Owner { get; private set; } = null!;
    public User Admin { get; private set; } = null!;
    public User Member { get; private set; } = null!;
    public User Stranger { get; private set; } = null!;

    private GroupTestScenario(FakeHttpContextAccessor httpContextAccessor, DomainServiceTestFactory.Graph graph)
    {
        _httpContextAccessor = httpContextAccessor;
        GroupService = graph.GroupService;
        MembershipService = graph.GroupMembershipService;
        BoardAccess = graph.GroupBoardAccessService;
        BoardService = graph.GroupBoardService;
        GroupRepository = graph.GroupRepository;
        GroupMemberRepository = graph.GroupMemberRepository;
        BoardRepository = graph.GroupBoardRepository;
        _userRepository = graph.UserRepository;
    }

    public static async Task<GroupTestScenario> CreateAsync(string nicknamePrefix)
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var scenario = new GroupTestScenario(http, graph);
        scenario.Owner = await scenario.CreateUserAsync($"{nicknamePrefix}_owner");
        scenario.Admin = await scenario.CreateUserAsync($"{nicknamePrefix}_admin");
        scenario.Member = await scenario.CreateUserAsync($"{nicknamePrefix}_member");
        scenario.Stranger = await scenario.CreateUserAsync($"{nicknamePrefix}_stranger");
        return scenario;
    }

    private async Task<User> CreateUserAsync(string nickname)
    {
        var user = new User($"{nickname}@test.com", "password", nickname);
        await _userRepository.CreateUserAsync(user);
        return user;
    }

    public void LoginAs(BoardAccessActor actor)
    {
        if (actor == BoardAccessActor.Anonymous)
        {
            LoginAnonymous();
            return;
        }

        LoginAs(ResolveActorUserId(actor));
    }

    public void LoginAs(GroupActorRole role) => LoginAs(ResolveActorUserId(role));

    public void LoginAs(long userId) =>
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    public void LoginAnonymous() => _httpContextAccessor.HttpContext = new DefaultHttpContext();

    public long ResolveActorUserId(BoardAccessActor actor) => actor switch
    {
        BoardAccessActor.Owner => Owner.Id,
        BoardAccessActor.Admin => Admin.Id,
        BoardAccessActor.Member => Member.Id,
        BoardAccessActor.Stranger => Stranger.Id,
        BoardAccessActor.Anonymous => throw new InvalidOperationException("Anonymous has no user id."),
        _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null),
    };

    public long ResolveActorUserId(GroupActorRole role) => role switch
    {
        GroupActorRole.Owner => Owner.Id,
        GroupActorRole.Admin => Admin.Id,
        GroupActorRole.Member => Member.Id,
        GroupActorRole.Stranger => Stranger.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public async Task<GroupResponseDto> SetupGroupAsync(
        GroupVisibility visibility,
        bool includeAdmin = true,
        bool includeMember = true)
    {
        LoginAs(Owner.Id);
        var group = await GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "TestGroup",
            Description = "test",
            Visibility = visibility,
        });
        if (includeAdmin)
            await MembershipService.AddMemberInternalAsync(group.Id, Admin.Id, GroupRole.Admin);
        if (includeMember)
            await MembershipService.AddMemberInternalAsync(group.Id, Member.Id, GroupRole.Member);
        return group;
    }

    public async Task<GroupBoard> SeedBoardAsync(long groupId, string name, BoardVisibility visibility)
    {
        var board = new GroupBoard(groupId, name, visibility);
        await BoardRepository.CreateAsync(board);
        return board;
    }

    public async Task AssertMemberRoleAsync(long groupId, long userId, GroupRole expected)
    {
        var member = await GroupMemberRepository.GetMemberAsync(groupId, userId);
        Assert.NotNull(member);
        Assert.Equal(expected, member!.Role);
    }
}

public enum BoardAccessActor
{
    Anonymous,
    Stranger,
    Member,
    Admin,
    Owner,
}
