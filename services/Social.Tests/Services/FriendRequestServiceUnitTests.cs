using Social.Dto;
using Social.Tests.Infrastructure;

namespace Social.Tests.Services;

public sealed class FriendRequestServiceUnitTests
{
    [Fact]
    public async Task SendRequest_CreatesPendingRequest()
    {
        using var graph = SocialServiceTestFactory.Create();
        var requester = graph.SeedUser("a");
        var addressee = graph.SeedUser("b");
        graph.SetCaller(requester);

        var outcome = await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee });

        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        Assert.NotNull(await graph.FriendRequestRepository.GetForUserPairAsync(requester, addressee));
    }

    [Fact]
    public async Task SendRequest_ToSelf_ThrowsArgumentException()
    {
        using var graph = SocialServiceTestFactory.Create();
        var user = graph.SeedUser("solo");
        graph.SetCaller(user);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.FriendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = user }));
    }

    [Fact]
    public async Task SendRequest_WhenAddresseeBlocked_ThrowsArgumentException()
    {
        using var graph = SocialServiceTestFactory.Create();
        var requester = graph.SeedUser("blocker");
        var blocked = graph.SeedUser("blocked");
        graph.SetCaller(requester);
        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.FriendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = blocked }));
    }
}
