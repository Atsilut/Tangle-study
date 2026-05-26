using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupBlacklistServiceUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly GroupService _groupService;
    private readonly GroupMembershipService _membershipService;
    private readonly GroupBlacklistService _blacklistService;
    private readonly GroupInvitationService _invitationService;
    private readonly GroupApplicationService _applicationService;
    private readonly FakeGroupBlacklistRepository _blacklistRepo;
    private readonly FakeGroupMemberRepository _memberRepo;
    private readonly FakeGroupInvitationRepository _invitationRepo;
    private readonly FakeGroupApplicationRepository _applicationRepo;
    private readonly FakeUserRepository _userRepository;

    public GroupBlacklistServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _groupService = graph.GroupService;
        _membershipService = graph.GroupMembershipService;
        _blacklistService = graph.GroupBlacklistService;
        _invitationService = graph.GroupInvitationService;
        _applicationService = graph.GroupApplicationService;
        _blacklistRepo = graph.GroupBlacklistRepository;
        _memberRepo = graph.GroupMemberRepository;
        _invitationRepo = graph.GroupInvitationRepository;
        _applicationRepo = graph.GroupApplicationRepository;
        _userRepository = graph.UserRepository;
    }

    private async Task<User> CreateUserAsync(string nickname)
    {
        var user = new User($"{nickname}@test.com", "password", nickname);
        await _userRepository.CreateUserAsync(user);
        return user;
    }

    private void LoginAs(long userId) =>
        _http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    private async Task<GroupResponseDto> CreateGroupAsync(long ownerId)
    {
        LoginAs(ownerId);
        return await _groupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "g", Description = "d", Visibility = GroupVisibility.Private,
        });
    }

    [Fact]
    public async Task Add_BlacklistsUser_KicksMember_AndClearsJoinArtifacts()
    {
        var owner = await CreateUserAsync("owner");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = target.Id });
        await _membershipService.AddMemberInternalAsync(group.Id, target.Id, GroupRole.Member);

        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id });

        Assert.True(await _blacklistRepo.ExistsAsync(group.Id, target.Id));
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, target.Id));
        Assert.Null(await _invitationRepo.GetPendingForUserAsync(group.Id, target.Id));
    }

    [Fact]
    public async Task Add_BlacklistsAdmin_WhenCallerIsOwner()
    {
        var owner = await CreateUserAsync("owner");
        var admin = await CreateUserAsync("admin");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = admin.Id });

        Assert.True(await _blacklistRepo.ExistsAsync(group.Id, admin.Id));
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, admin.Id));
    }

    [Fact]
    public async Task Add_Throws_WhenUserAlreadyBlacklisted()
    {
        var owner = await CreateUserAsync("owner");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id });

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id }));
    }

    [Fact]
    public async Task Add_Throws_WhenCallerIsMember()
    {
        var owner = await CreateUserAsync("owner");
        var member = await CreateUserAsync("member");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        LoginAs(member.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id }));
    }

    [Fact]
    public async Task Add_Throws_WhenCallerIsAdmin()
    {
        var owner = await CreateUserAsync("owner");
        var admin = await CreateUserAsync("admin");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);

        LoginAs(admin.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id }));
    }

    [Fact]
    public async Task Remove_Throws_WhenCallerIsAdmin()
    {
        var owner = await CreateUserAsync("owner");
        var admin = await CreateUserAsync("admin");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id });

        LoginAs(admin.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _blacklistService.RemoveAsync(group.Id, target.Id));
    }

    [Fact]
    public async Task EnsureNotBlacklisted_Throws_WhenBlacklisted()
    {
        var owner = await CreateUserAsync("owner");
        var target = await CreateUserAsync("target");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = target.Id });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _blacklistService.EnsureNotBlacklistedAsync(group.Id, target.Id));
    }
}
