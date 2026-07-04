using Social.Friendships.Dto;
using Social.Tests.Infrastructure;

namespace Social.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    [Fact]
    public async Task GetFriends_ReturnsFriend_AfterAccept()
    {
        using var graph = SocialServiceTestFactory.Create();
        var userA = graph.SeedUser("a");
        var userB = graph.SeedUser("b");
        graph.SetCaller(userA);
        await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = userB });
        graph.SetCaller(userB);
        var pending = await graph.FriendRequestService.GetPendingAsync();
        var requestId = pending!.Single(p => p.OtherUserId == userA).Id;
        await graph.FriendRequestService.AcceptRequestAsync(requestId);
        graph.SetCaller(userA);

        var friends = await graph.FriendshipService.GetMyFriendsAsync();

        Assert.NotNull(friends);
        Assert.Contains(friends!, f => f.OtherUserId == userB);
    }
}
