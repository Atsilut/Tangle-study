using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupMembershipServiceUnitTests
{
    [Fact]
    public async Task HandleUserDeletionAsync_DeletesGroup_WhenOwnerIsSoleMember()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.MonolithAccess.SeedUser("owner");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });

        await graph.GroupMembershipService.HandleUserDeletionAsync(ownerId);

        Assert.False(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
        Assert.Empty(await graph.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    [Fact]
    public async Task HandleUserDeletionAsync_TransfersOwnershipToAdmin_WhenOwnerDeleted()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.MonolithAccess.SeedUser("owner");
        var adminId = graph.MonolithAccess.SeedUser("admin");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, adminId, GroupRole.Admin);

        await graph.GroupMembershipService.HandleUserDeletionAsync(ownerId);

        Assert.Null(await graph.GroupMemberRepository.GetMemberAsync(group.Id, ownerId));
        var adminMember = await graph.GroupMemberRepository.GetMemberAsync(group.Id, adminId);
        Assert.NotNull(adminMember);
        Assert.Equal(GroupRole.Owner, adminMember.Role);
        Assert.True(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
    }

    [Fact]
    public async Task GetMembers_ReturnsMembers_WhenCallerIsMember()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.MonolithAccess.SeedUser("owner");
        var memberId = graph.MonolithAccess.SeedUser("member");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, memberId, GroupRole.Member);
        http.HttpContext = FakeHttpContextAccessor.ContextFor(memberId);

        var members = await graph.GroupMembershipService.GetMembersAsync(group.Id);

        Assert.NotNull(members);
        Assert.True(members.Count >= 2);
    }
}
