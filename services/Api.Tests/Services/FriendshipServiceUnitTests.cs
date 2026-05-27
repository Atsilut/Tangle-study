using Api.Domain.Friendships.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    [Fact]
    public async Task GetFriends_ReturnsFriend_AfterAccept()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var userA = await CreateUserAsync(graph.UserRepository, "a");
        var userB = await CreateUserAsync(graph.UserRepository, "b");
        http.HttpContext = ContextFor(userA.Id);
        await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = userB.Id });
        http.HttpContext = ContextFor(userB.Id);
        var pending = await graph.FriendRequestService.GetPendingAsync();
        var requestId = pending!.Single(p => p.OtherUserId == userA.Id).Id;
        await graph.FriendRequestService.AcceptRequestAsync(requestId);
        http.HttpContext = ContextFor(userA.Id);

        // Act
        var friends = await graph.FriendshipService.GetMyFriendsAsync();

        // Assert
        Assert.NotNull(friends);
        Assert.Contains(friends!, f => f.OtherUserId == userB.Id);
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new User($"{nickname}@test.com", "password", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
