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

public sealed class GroupJoinPolicyUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly GroupService _groupService;
    private readonly GroupJoinService _joinService;
    private readonly GroupApplicationService _applicationService;
    private readonly GroupMembershipService _membershipService;
    private readonly FakeGroupApplicationRepository _applicationRepo;
    private readonly FakeUserRepository _userRepository;

    public GroupJoinPolicyUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _groupService = graph.GroupService;
        _joinService = graph.GroupJoinService;
        _applicationService = graph.GroupApplicationService;
        _membershipService = graph.GroupMembershipService;
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

    private async Task<GroupResponseDto> CreateGroupAsync(long ownerId, GroupJoinPolicy joinPolicy)
    {
        LoginAs(ownerId);
        return await _groupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "g",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = joinPolicy,
        });
    }

    [Fact]
    public async Task CreateGroup_PersistsJoinPolicy()
    {
        var owner = await CreateUserAsync("owner");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Open);

        Assert.Equal(GroupJoinPolicy.Open, group.JoinPolicy);
    }

    [Fact]
    public async Task Join_OpenPolicy_AddsMemberImmediately()
    {
        var owner = await CreateUserAsync("owner");
        var joiner = await CreateUserAsync("joiner");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Open);

        LoginAs(joiner.Id);
        await _joinService.JoinAsync(group.Id);

        Assert.True(await _membershipService.IsMemberAsync(group.Id, joiner.Id));
        Assert.Empty(await _applicationRepo.GetPendingByGroupAsync(group.Id));
    }

    [Fact]
    public async Task Join_RequestablePolicy_Throws()
    {
        var owner = await CreateUserAsync("owner");
        var joiner = await CreateUserAsync("joiner");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Requestable);

        LoginAs(joiner.Id);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _joinService.JoinAsync(group.Id));
        Assert.Contains("application", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Join_InvitationOnlyPolicy_Throws()
    {
        var owner = await CreateUserAsync("owner");
        var joiner = await CreateUserAsync("joiner");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.InvitationOnly);

        LoginAs(joiner.Id);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _joinService.JoinAsync(group.Id));
        Assert.Contains("invitation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_OpenPolicy_Throws()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Open);

        LoginAs(applicant.Id);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _applicationService.ApplyAsync(group.Id));
        Assert.Contains("join endpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_InvitationOnlyPolicy_Throws()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.InvitationOnly);

        LoginAs(applicant.Id);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _applicationService.ApplyAsync(group.Id));
        Assert.Contains("invitation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_RequestablePolicy_CreatesApplication()
    {
        var owner = await CreateUserAsync("owner");
        var applicant = await CreateUserAsync("applicant");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Requestable);

        LoginAs(applicant.Id);
        var result = await _applicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.False(await _membershipService.IsMemberAsync(group.Id, applicant.Id));
    }

    [Fact]
    public async Task Join_WhenAlreadyMember_Throws()
    {
        var owner = await CreateUserAsync("owner");
        var group = await CreateGroupAsync(owner.Id, GroupJoinPolicy.Open);

        LoginAs(owner.Id);
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => _joinService.JoinAsync(group.Id));
    }
}
