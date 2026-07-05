using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupServiceUnitTests
{
    [Fact]
    public async Task CreateGroup_AddsCreatorAsOwner()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.InMemoryUser.SeedUser("owner");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);

        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Devs",
            Description = "desc",
            Visibility = GroupVisibility.Private,
        });

        Assert.Equal(1, group.MemberCount);
        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, ownerId);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Owner, member.Role);
    }

    [Fact]
    public async Task GetGroupNamesByIdsAsync_ReturnsNamesForExistingGroups()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.InMemoryUser.SeedUser("owner");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var first = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "First",
            Description = "d",
            Visibility = GroupVisibility.Public,
        });
        var second = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Second",
            Description = "d",
            Visibility = GroupVisibility.Public,
        });

        var names = await graph.GroupService.GetGroupNamesByIdsAsync([first.Id, second.Id, 999]);

        Assert.Equal(2, names.Count);
        Assert.Equal("First", names[first.Id]);
        Assert.Equal("Second", names[second.Id]);
        Assert.False(names.ContainsKey(999));
    }

    [Fact]
    public async Task GetGroup_ReturnsGroup_WhenPrivateAndNotMember()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = GroupServiceTestFactory.Create(http);
        var ownerId = graph.InMemoryUser.SeedUser("owner");
        var strangerId = graph.InMemoryUser.SeedUser("stranger");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(ownerId);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Private",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        http.HttpContext = FakeHttpContextAccessor.ContextFor(strangerId);

        var dto = await graph.GroupService.GetGroupAsync(group.Id);

        Assert.Equal(group.Id, dto.Id);
        Assert.Equal("Private", dto.Name);
        Assert.True(dto.IsLimitedProfile);
        Assert.Equal(string.Empty, dto.Description);
    }
}
