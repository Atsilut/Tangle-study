using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class GroupBoardServiceUnitTests
{
    [Fact]
    public async Task CreateAsync_UsesMembersOnlyDefaultOnPrivateGroup()
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
        var board = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "board",
            Description = "desc",
        });

        // Assert
        Assert.Equal(BoardVisibility.MembersOnly, board.Visibility);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyBoardsVisibleToMember()
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

        var membersBoard = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "members",
            Description = "desc",
            Visibility = BoardVisibility.MembersOnly,
        });
        var adminBoard = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "admins",
            Description = "desc",
            Visibility = BoardVisibility.AdminOnly,
        });

        http.HttpContext = ServiceTestHelpers.ContextFor(member.Id);

        // Act
        var boards = await graph.GroupBoardService.ListAsync(group.Id);

        // Assert
        Assert.NotNull(boards);
        Assert.Single(boards);
        Assert.Equal(membersBoard.Id, boards[0].Id);
        Assert.DoesNotContain(boards, board => board.Id == adminBoard.Id);
    }
}
