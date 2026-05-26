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

public sealed class GroupApplicationServiceUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly GroupService _groupService;
    private readonly GroupMembershipService _membershipService;
    private readonly GroupApplicationService _applicationService;
    private readonly GroupInvitationService _invitationService;
    private readonly GroupBlacklistService _blacklistService;
    private readonly FakeGroupApplicationRepository _applicationRepo;
    private readonly FakeGroupInvitationRepository _invitationRepo;
    private readonly FakeGroupMemberRepository _memberRepo;
    private readonly FakeUserRepository _userRepository;

    public GroupApplicationServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _groupService = graph.GroupService;
        _membershipService = graph.GroupMembershipService;
        _applicationService = graph.GroupApplicationService;
        _invitationService = graph.GroupInvitationService;
        _blacklistService = graph.GroupBlacklistService;
        _applicationRepo = graph.GroupApplicationRepository;
        _invitationRepo = graph.GroupInvitationRepository;
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
            Name = "g",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = GroupJoinPolicy.Requestable,
        });
    }

    [Fact]
    public async Task Apply_CreatesPendingApplication()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(applicant.Id);
        var result = await _applicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.True(result.Application.IsPending);
        Assert.Equal(applicant.Id, result.Application.ApplicantId);
    }

    [Fact]
    public async Task Apply_AfterPendingInvitation_AddsMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _invitationService.InviteAsync(group.Id, new GroupInvitationCreateRequestDto { InviteeId = applicant.Id });

        LoginAs(applicant.Id);
        var result = await _applicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, result.Outcome);
        Assert.Null(result.Application);
        var member = await _memberRepo.GetMemberAsync(group.Id, applicant.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
        Assert.Null(await _invitationRepo.GetPendingForUserAsync(group.Id, applicant.Id));
        Assert.Null(await _applicationRepo.GetPendingForUserAsync(group.Id, applicant.Id));
    }

    [Fact]
    public async Task Apply_ReturnsCreated_WhenDuplicatePendingApplication()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(applicant.Id);
        await _applicationService.ApplyAsync(group.Id);
        var result = await _applicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(result.Application);
    }

    [Fact]
    public async Task Apply_Throws_WhenAlreadyMember()
    {
        var owner = await CreateUserAsync("owner");
        var group = await CreateGroupAsync(owner.Id);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => _applicationService.ApplyAsync(group.Id));
    }

    [Fact]
    public async Task Approve_AddsApplicantAsMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.ApproveAsync(application.Id);

        var member = await _memberRepo.GetMemberAsync(group.Id, applicant.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
        Assert.Null(await _applicationRepo.GetByIdAsync(application.Id));
    }

    [Fact]
    public async Task Approve_IsIdempotent_WhenApplicantAlreadyMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        await _membershipService.AddMemberInternalAsync(group.Id, applicant.Id, GroupRole.Member);

        LoginAs(owner.Id);
        await _applicationService.ApproveAsync(application.Id);
    }

    [Fact]
    public async Task Approve_ThrowsUnauthorized_WhenCalledByMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var member = await CreateUserAsync("member");
        var group = await CreateGroupAsync(owner.Id);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(member.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _applicationService.ApproveAsync(application.Id));
    }

    [Fact]
    public async Task Reject_DeletesApplicationAndDoesNotAddMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.RejectAsync(application.Id);

        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, applicant.Id));
        Assert.Null(await _applicationRepo.GetByIdAsync(application.Id));
    }

    [Fact]
    public async Task Cancel_DeletesApplication_WhenCalledByApplicant()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        await _applicationService.CancelAsync(application.Id);

        Assert.Null(await _applicationRepo.GetByIdAsync(application.Id));
    }

    [Fact]
    public async Task Cancel_ThrowsUnauthorized_WhenCalledByOwner()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _applicationService.CancelAsync(application.Id));
    }

    [Fact]
    public async Task GetPendingByGroup_ReturnsList_ForOwner()
    {
        var owner = await CreateUserAsync("owner");
        var applicantA = await CreateUserAsync("applicantA");
        var applicantB = await CreateUserAsync("applicantB");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicantA.Id);
        await _applicationService.ApplyAsync(group.Id);
        LoginAs(applicantB.Id);
        await _applicationService.ApplyAsync(group.Id);

        LoginAs(owner.Id);
        var list = await _applicationService.GetPendingByGroupAsync(group.Id);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task Ignore_SetsApplicationNotPending()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.IgnoreAsync(application.Id);

        var stored = await _applicationRepo.GetByIdAsync(application.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsPending);
    }

    [Fact]
    public async Task Apply_ReturnsCreatedWithoutReactivate_WhenResendingAfterOwnerIgnored()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.IgnoreAsync(application.Id);

        LoginAs(applicant.Id);
        var result = await _applicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.True(result.Application.IsPending);
        Assert.Equal(application.Id, result.Application.Id);
        Assert.False((await _applicationRepo.GetByIdAsync(application.Id))!.IsPending);
    }

    [Fact]
    public async Task GetMyApplications_StillShowsOutgoingAsPending_WhenOwnerIgnored()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.IgnoreAsync(application.Id);

        LoginAs(applicant.Id);
        var list = await _applicationService.GetMyApplicationsAsync();

        var only = Assert.Single(list!);
        Assert.Equal(application.Id, only.Id);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task Approve_WhenIgnoredIncoming_AddsMember()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);
        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _applicationService.IgnoreAsync(application.Id);
        await _applicationService.ApproveAsync(application.Id);

        Assert.NotNull(await _memberRepo.GetMemberAsync(group.Id, applicant.Id));
    }

    [Fact]
    public async Task Apply_ThrowsArgument_WhenApplicantBlacklisted()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = applicant.Id });

        LoginAs(applicant.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => _applicationService.ApplyAsync(group.Id));
    }

    [Fact]
    public async Task Approve_ThrowsNotFound_WhenApplicationRemovedByBlacklist()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id);

        LoginAs(applicant.Id);
        var application = (await _applicationService.ApplyAsync(group.Id)).Application!;

        LoginAs(owner.Id);
        await _blacklistService.AddAsync(group.Id, new GroupBlacklistCreateRequestDto { UserId = applicant.Id });

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _applicationService.ApproveAsync(application.Id));
        Assert.Null(await _memberRepo.GetMemberAsync(group.Id, applicant.Id));
    }
}
