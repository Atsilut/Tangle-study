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

public sealed class GroupMembershipServiceUnitTests
{
    private readonly GroupService _groupService;
    private readonly GroupMembershipService _membershipService;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeGroupMemberRepository _groupMemberRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public GroupMembershipServiceUnitTests()
    {
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_httpContextAccessor);
        _groupService = graph.GroupService;
        _membershipService = graph.GroupMembershipService;
        _userRepository = graph.UserRepository;
        _groupMemberRepository = graph.GroupMemberRepository;
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

    private void LoginAs(long userId) =>
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    private static GroupCreateRequestDto MakeCreateRequest(string name = "g", GroupVisibility visibility = GroupVisibility.Private) =>
        new() { Name = name, Description = "d", Visibility = visibility };

    // --- ROLE ---

    [Fact]
    public async Task UpdateRole_PromotesMemberToAdmin()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        // Act
        var result = await _membershipService.UpdateRoleAsync(group.Id, member.Id, new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin });

        // Assert
        Assert.Equal(GroupRole.Admin, result.Role);
    }

    [Fact]
    public async Task UpdateRole_ThrowsUnauthorized_WhenCallerIsAdmin()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var admin = await CreateTestUserAsync("admin");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        LoginAs(admin.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _membershipService.UpdateRoleAsync(group.Id, member.Id, new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin }));
    }

    [Fact]
    public async Task UpdateRole_ThrowsArgument_WhenTargetIsOwner()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _membershipService.UpdateRoleAsync(group.Id, owner.Id, new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin }));
    }

    // --- REMOVE ---

    [Fact]
    public async Task RemoveMember_RemovesMembership_WhenSelfLeave()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        LoginAs(member.Id);

        // Act
        await _membershipService.RemoveMemberAsync(group.Id, member.Id);

        // Assert
        Assert.Null(await _groupMemberRepository.GetMemberAsync(group.Id, member.Id));
    }

    [Fact]
    public async Task RemoveMember_ThrowsArgument_WhenOwnerLeaves()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _membershipService.RemoveMemberAsync(group.Id, owner.Id));
    }

    [Fact]
    public async Task RemoveMember_RemovesMember_WhenAdminKicksMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var admin = await CreateTestUserAsync("admin");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        LoginAs(admin.Id);

        // Act
        await _membershipService.RemoveMemberAsync(group.Id, member.Id);

        // Assert
        Assert.Null(await _groupMemberRepository.GetMemberAsync(group.Id, member.Id));
    }

    [Fact]
    public async Task RemoveMember_RemovesAdmin_WhenOwnerKicksAdmin()
    {
        var owner = await CreateTestUserAsync("owner");
        var admin = await CreateTestUserAsync("admin");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);

        await _membershipService.RemoveMemberAsync(group.Id, admin.Id);

        Assert.Null(await _groupMemberRepository.GetMemberAsync(group.Id, admin.Id));
    }

    [Fact]
    public async Task RemoveMember_ThrowsUnauthorized_WhenAdminKicksAnotherAdmin()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var adminA = await CreateTestUserAsync("adminA");
        var adminB = await CreateTestUserAsync("adminB");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, adminA.Id, GroupRole.Admin);
        await _membershipService.AddMemberInternalAsync(group.Id, adminB.Id, GroupRole.Admin);
        LoginAs(adminA.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _membershipService.RemoveMemberAsync(group.Id, adminB.Id));
    }

    [Fact]
    public async Task RemoveMember_ThrowsUnauthorized_WhenMemberKicksAnother()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var memberA = await CreateTestUserAsync("memberA");
        var memberB = await CreateTestUserAsync("memberB");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, memberA.Id, GroupRole.Member);
        await _membershipService.AddMemberInternalAsync(group.Id, memberB.Id, GroupRole.Member);
        LoginAs(memberA.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _membershipService.RemoveMemberAsync(group.Id, memberB.Id));
    }

    // --- GET MEMBERS ---

    [Fact]
    public async Task GetMembers_ReturnsList_OrderedByRoleThenJoined()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var admin = await CreateTestUserAsync("admin");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Public));
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        // Act
        var members = await _membershipService.GetMembersAsync(group.Id);

        // Assert
        Assert.NotNull(members);
        Assert.Equal(3, members!.Count);
        Assert.Equal(GroupRole.Owner, members[0].Role);
        Assert.Equal(GroupRole.Admin, members[1].Role);
        Assert.Equal(GroupRole.Member, members[2].Role);
    }

    [Fact]
    public async Task GetMembers_ThrowsNotFound_WhenPrivateGroupAndNonMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var stranger = await CreateTestUserAsync("stranger");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Private));
        LoginAs(stranger.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _membershipService.GetMembersAsync(group.Id));
    }
}
