using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupBoardServiceUnitTests
{
    [Fact]
    public async Task CreateAsync_UsesMembersOnlyDefaultOnPrivateGroup()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = ContextFor(owner.Id);
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

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<Api.Domain.Users.Domain.User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new Api.Domain.Users.Domain.User($"{nickname}@test.com", "password", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
