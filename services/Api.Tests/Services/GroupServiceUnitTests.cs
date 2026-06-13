using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class GroupServiceUnitTests
{
    [Fact]
    public async Task CreateGroup_AddsCreatorAsOwner()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);

        // Act
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Devs",
            Description = "desc",
            Visibility = GroupVisibility.Private,
        });

        // Assert
        Assert.Equal(1, group.MemberCount);
        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, owner.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Owner, member.Role);
    }

    [Fact]
    public async Task GetGroupNamesByIdsAsync_ReturnsNamesForExistingGroups()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
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

        // Act
        var names = await graph.GroupService.GetGroupNamesByIdsAsync([first.Id, second.Id, 999]);

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Equal("First", names[first.Id]);
        Assert.Equal("Second", names[second.Id]);
        Assert.False(names.ContainsKey(999));
    }

    [Fact]
    public async Task GetGroup_ReturnsGroup_WhenPrivateAndNotMember()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var stranger = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "stranger");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Private",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        http.HttpContext = ServiceTestHelpers.ContextFor(stranger.Id);

        // Act
        var dto = await graph.GroupService.GetGroupAsync(group.Id);

        // Assert
        Assert.Equal(group.Id, dto.Id);
        Assert.Equal("Private", dto.Name);
    }
}
