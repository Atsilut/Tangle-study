using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Services;

public sealed class GroupBoardServiceUnitTests
{
    [Fact]
    public async Task CreateAsync_UsesMembersOnlyDefaultOnPrivateGroup()
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

        var board = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "board",
            Description = "desc",
        });

        Assert.Equal(BoardVisibility.MembersOnly, board.Visibility);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyBoardsVisibleToMember()
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

        http.HttpContext = FakeHttpContextAccessor.ContextFor(memberId);

        var boards = await graph.GroupBoardService.ListAsync(group.Id);

        Assert.NotNull(boards);
        Assert.Single(boards);
        Assert.Equal(membersBoard.Id, boards[0].Id);
        Assert.DoesNotContain(boards, board => board.Id == adminBoard.Id);
    }
}
