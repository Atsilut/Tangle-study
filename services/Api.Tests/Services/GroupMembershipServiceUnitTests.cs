using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class GroupMembershipServiceUnitTests
{
    [Fact]
    public async Task HandleUserDeletionAsync_DeletesGroup_WhenOwnerIsSoleMember()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });

        // Act
        await graph.GroupMembershipService.HandleUserDeletionAsync(owner.Id);

        // Assert
        Assert.False(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
        Assert.Empty(await graph.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    [Fact]
    public async Task HandleUserDeletionAsync_TransfersOwnershipToAdmin_WhenOwnerDeleted()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var admin = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "admin");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);

        // Act
        await graph.GroupMembershipService.HandleUserDeletionAsync(owner.Id);

        // Assert
        Assert.Null(await graph.GroupMemberRepository.GetMemberAsync(group.Id, owner.Id));
        var adminMember = await graph.GroupMemberRepository.GetMemberAsync(group.Id, admin.Id);
        Assert.NotNull(adminMember);
        Assert.Equal(GroupRole.Owner, adminMember.Role);
        Assert.True(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
    }

    [Fact]
    public async Task GetMembers_ReturnsMembers_WhenCallerIsMember()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var member = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "member");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        http.HttpContext = ServiceTestHelpers.ContextFor(member.Id);

        // Act
        var members = await graph.GroupMembershipService.GetMembersAsync(group.Id);

        // Assert
        Assert.NotNull(members);
        Assert.True(members!.Count >= 2);
    }
}
