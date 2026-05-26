using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupInvitationServiceUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly GroupService _groupService;
    private readonly GroupMembershipService _membershipService;
    private readonly GroupApplicationService _applicationService;
    private readonly GroupInvitationService _invitationService;
    private readonly UserBlockService _userBlockService;
    private readonly GroupBlacklistService _blacklistService;
    private readonly FakeGroupInvitationRepository _invitationRepo;
    private readonly FakeGroupApplicationRepository _applicationRepo;
    private readonly FakeGroupMemberRepository _memberRepo;
    private readonly FakeUserRepository _userRepository;

    public GroupInvitationServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _groupService = graph.GroupService;
        _membershipService = graph.GroupMembershipService;
        _applicationService = graph.GroupApplicationService;
        _invitationService = graph.GroupInvitationService;
        _userBlockService = graph.UserBlockService;
        _blacklistService = graph.GroupBlacklistService;
        _invitationRepo = graph.GroupInvitationRepository;
        _applicationRepo = graph.GroupApplicationRepository;
        _memberRepo = graph.GroupMemberRepository;
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
    public async Task Invite_CreatesPendingInvitation()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(result.Invitation);
        Assert.True(result.Invitation.IsPending);
        Assert.Equal(invitee.Id, result.Invitation.InviteeId);
    }

    [Fact]
    public async Task Invite_AfterPendingApplication_AddsMember()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(invitee.Id);
        await _applicationService.ApplyAsync(group.Id);

        LoginAs(owner.Id);
        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication, result.Outcome);
        Assert.Null(result.Invitation);
        var member = await _memberRepo.GetMemberAsync(group.Id, invitee.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
        Assert.Null(await _applicationRepo.GetPendingForUserAsync(group.Id, invitee.Id));
        Assert.Null(await _invitationRepo.GetPendingForUserAsync(group.Id, invitee.Id));
    }

    [Fact]
    public async Task Invite_ReturnsCreated_WhenDuplicatePendingInvitation()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(result.Invitation);
    }

    [Fact]
    public async Task Invite_StoresAtMostOneRowPerGroupInvitee()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        var pending = await _invitationRepo.GetPendingIncomingForInviteeAsync(invitee.Id);
        Assert.Single(pending);
    }

    [Fact]
    public async Task Invite_ThrowsUnauthorized_WhenCallerNotAdmin()
    {
        var owner = await CreateUserAsync("owner");
        var member = await CreateUserAsync("member");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        LoginAs(member.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id }));
    }

    [Fact]
    public async Task Invite_ThrowsConflict_WhenAlreadyMember()
    {
        var owner = await CreateUserAsync("owner");
        var member = await CreateUserAsync("member");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = member.Id }));
    }

    [Fact]
    public async Task Accept_AddsInviteeAsMember()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.AcceptAsync(invitation.Id);

        var member = await _memberRepo.GetMemberAsync(group.Id, invitee.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
        Assert.Null(await _invitationRepo.GetByIdAsync(invitation.Id));
    }

    [Fact]
    public async Task Accept_ThrowsUnauthorized_WhenCallerNotInvitee()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var stranger = await CreateUserAsync("stranger");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(stranger.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _invitationService.AcceptAsync(invitation.Id));
    }

    [Fact]
    public async Task Reject_DeletesInvitationAndDoesNotAddMember()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.RejectAsync(invitation.Id);

        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, invitee.Id));
        Assert.Null(await _invitationRepo.GetByIdAsync(invitation.Id));
    }

    [Fact]
    public async Task Reject_AllowsReinvite()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.RejectAsync(invitation.Id);

        LoginAs(owner.Id);
        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });
        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
    }

    [Fact]
    public async Task Cancel_DeletesInvitation_WhenCalledByInviter()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        await _invitationService.CancelAsync(invitation.Id);

        Assert.Null(await _invitationRepo.GetByIdAsync(invitation.Id));
    }

    [Fact]
    public async Task Cancel_ThrowsUnauthorized_WhenCalledByStranger()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var stranger = await CreateUserAsync("stranger");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(stranger.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _invitationService.CancelAsync(invitation.Id));
    }

    [Fact]
    public async Task Ignore_SetsInvitationNotPending()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.IgnoreAsync(invitation.Id);

        var stored = await _invitationRepo.GetByIdAsync(invitation.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsPending);
    }

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutReactivate_WhenResendingAfterInviteeIgnored()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.IgnoreAsync(invitation.Id);

        LoginAs(owner.Id);
        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(result.Invitation);
        Assert.True(result.Invitation.IsPending);
        Assert.Equal(invitation.Id, result.Invitation.Id);
        Assert.False((await _invitationRepo.GetByIdAsync(invitation.Id))!.IsPending);
    }

    [Fact]
    public async Task GetMyPending_StillShowsOutgoingAsPending_WhenInviteeIgnored()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.IgnoreAsync(invitation.Id);

        LoginAs(owner.Id);
        var list = await _invitationService.GetMyPendingAsync();

        var only = Assert.Single(list!);
        Assert.Equal(invitation.Id, only.Id);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task GetIgnoredIncoming_ListsIgnoredIncoming_ForInvitee()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.IgnoreAsync(invitation.Id);

        var ignored = await _invitationService.GetIgnoredIncomingAsync();
        var only = Assert.Single(ignored!);
        Assert.Equal(invitation.Id, only.Id);
        Assert.False(only.IsPending);
        Assert.True(only.IsIncoming);
    }

    [Fact]
    public async Task Accept_WhenIgnoredIncoming_AddsMember()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _invitationService.IgnoreAsync(invitation.Id);
        await _invitationService.AcceptAsync(invitation.Id);

        Assert.NotNull(await _memberRepo.GetMemberAsync(group.Id, invitee.Id));
    }

    [Fact]
    public async Task Invite_ThrowsArgument_WhenInviteeBlacklisted()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = invitee.Id });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id }));
    }

    [Fact]
    public async Task Invite_ThrowsArgument_WhenInviterBlockedInvitee()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = invitee.Id });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id }));
    }

    [Fact]
    public async Task Invite_CreatesNonPendingInvitation_WhenInviteeBlockedInviter()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(invitee.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = owner.Id });

        LoginAs(owner.Id);
        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        var stored = await _invitationRepo.GetForUserAsync(group.Id, invitee.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsPending);
    }

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutJoin_WhenReciprocalApplicationAndBlockExists()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(invitee.Id);
        await _applicationService.ApplyAsync(group.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = owner.Id });

        LoginAs(owner.Id);
        var result = await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, invitee.Id));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenInviteeBlockedInviter()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(invitee.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = owner.Id });
        await _invitationService.IgnoreAsync(invitation.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => _invitationService.AcceptAsync(invitation.Id));
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, invitee.Id));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenInviterBlockedInvitee()
    {
        var owner = await CreateUserAsync("owner");
        var invitee = await CreateUserAsync("invitee");
        var group = await CreateGroupAsync(owner.Id);
        var invitation = (await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = invitee.Id })).Invitation!;

        LoginAs(owner.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = invitee.Id });

        LoginAs(invitee.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => _invitationService.AcceptAsync(invitation.Id));
        Assert.NotNull(await _invitationRepo.GetByIdAsync(invitation.Id));
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, invitee.Id));
    }
}
