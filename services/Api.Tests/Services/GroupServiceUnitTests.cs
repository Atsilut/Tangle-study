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

public sealed class GroupServiceUnitTests
{
    private readonly GroupService _groupService;
    private readonly GroupMembershipService _membershipService;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeGroupRepository _groupRepository;
    private readonly FakeGroupMemberRepository _groupMemberRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public GroupServiceUnitTests()
    {
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_httpContextAccessor);
        _groupService = graph.GroupService;
        _membershipService = graph.GroupMembershipService;
        _userRepository = graph.UserRepository;
        _groupRepository = graph.GroupRepository;
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

    private static GroupCreateRequestDto MakeCreateRequest(string name = "Devs", GroupVisibility visibility = GroupVisibility.Private) =>
        new() { Name = name, Description = "desc", Visibility = visibility };

    // --- CREATE ---

    [Fact]
    public async Task CreateGroup_AddsCreatorAsOwner()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        LoginAs(owner.Id);

        // Act
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());

        // Assert
        Assert.Equal(1, group.MemberCount);
        var member = await _groupMemberRepository.GetMemberAsync(group.Id, owner.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Owner, member!.Role);
    }

    // --- GET ---

    [Fact]
    public async Task GetGroup_ThrowsNotFound_WhenPrivateAndNotMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var stranger = await CreateTestUserAsync("stranger");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Private));
        LoginAs(stranger.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _groupService.GetGroupAsync(group.Id));
    }

    [Fact]
    public async Task GetGroup_ReturnsGroup_WhenPublicAndNotMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var stranger = await CreateTestUserAsync("stranger");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Public));
        LoginAs(stranger.Id);

        // Act
        var dto = await _groupService.GetGroupAsync(group.Id);

        // Assert
        Assert.Equal(group.Id, dto.Id);
        Assert.Equal(GroupVisibility.Public, dto.Visibility);
    }

    // --- UPDATE ---

    [Fact]
    public async Task UpdateGroup_UpdatesDetails_WhenOwner()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());

        // Act
        var updated = await _groupService.UpdateGroupAsync(new GroupPatchRequestDto
        {
            Id = group.Id,
            Name = "Renamed",
            Description = "new desc",
            Visibility = GroupVisibility.Public,
        });

        // Assert
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(GroupVisibility.Public, updated.Visibility);
    }

    [Fact]
    public async Task UpdateGroup_ThrowsUnauthorized_WhenMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        LoginAs(member.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _groupService.UpdateGroupAsync(new GroupPatchRequestDto
            {
                Id = group.Id,
                Name = "x",
                Description = "y",
                Visibility = GroupVisibility.Public,
            }));
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteGroup_RemovesGroupAndMemberships()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var member = await CreateTestUserAsync("member");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);

        // Act
        await _groupService.DeleteGroupAsync(group.Id);

        // Assert
        Assert.Null(await _groupRepository.GetGroupByIdAsync(group.Id));
        Assert.Empty(await _groupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    [Fact]
    public async Task DeleteGroup_ThrowsUnauthorized_WhenAdmin()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var admin = await CreateTestUserAsync("admin");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);
        LoginAs(admin.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _groupService.DeleteGroupAsync(group.Id));
    }

    // --- TRANSFER ---

    [Fact]
    public async Task TransferOwnership_SwapsRoles_WhenTargetIsMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var heir = await CreateTestUserAsync("heir");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());
        await _membershipService.AddMemberInternalAsync(group.Id, heir.Id, GroupRole.Member);

        // Act
        await _groupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = heir.Id });

        // Assert
        var ownerMember = await _groupMemberRepository.GetMemberAsync(group.Id, owner.Id);
        var heirMember = await _groupMemberRepository.GetMemberAsync(group.Id, heir.Id);
        Assert.Equal(GroupRole.Admin, ownerMember!.Role);
        Assert.Equal(GroupRole.Owner, heirMember!.Role);
    }

    [Fact]
    public async Task TransferOwnership_ThrowsArgument_WhenTargetIsNotMember()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var stranger = await CreateTestUserAsync("stranger");
        LoginAs(owner.Id);
        var group = await _groupService.CreateGroupAsync(MakeCreateRequest());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _groupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = stranger.Id }));
    }
}
