using Api.Domain.Friendships.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    [Fact]
    public async Task GetFriends_ReturnsFriend_AfterAccept()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var userA = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "a");
        var userB = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "b");
        http.HttpContext = ServiceTestHelpers.ContextFor(userA.Id);
        await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = userB.Id });
        http.HttpContext = ServiceTestHelpers.ContextFor(userB.Id);
        var pending = await graph.FriendRequestService.GetPendingAsync();
        var requestId = pending!.Single(p => p.OtherUserId == userA.Id).Id;
        await graph.FriendRequestService.AcceptRequestAsync(requestId);
        http.HttpContext = ServiceTestHelpers.ContextFor(userA.Id);

        // Act
        var friends = await graph.FriendshipService.GetMyFriendsAsync();

        // Assert
        Assert.NotNull(friends);
        Assert.Contains(friends!, f => f.OtherUserId == userB.Id);
    }
}
