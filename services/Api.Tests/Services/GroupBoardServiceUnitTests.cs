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
}
