using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;
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
    public async Task GetGroup_ThrowsNotFound_WhenPrivateAndNotMember()
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

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => graph.GroupService.GetGroupAsync(group.Id));
    }
}
