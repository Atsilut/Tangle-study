using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class FriendRequestServiceUnitTests
{
    [Fact]
    public async Task SendRequest_CreatesPendingRequest()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var requester = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "a");
        var addressee = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "b");
        http.HttpContext = ServiceTestHelpers.ContextFor(requester.Id);

        // Act
        var outcome = await graph.FriendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        Assert.NotNull(await graph.FriendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task SendRequest_ToSelf_ThrowsArgumentException()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "solo");
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.FriendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = user.Id }));
    }

    [Fact]
    public async Task SendRequest_WhenAddresseeBlocked_ThrowsArgumentException()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var requester = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "blocker");
        var blocked = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "blocked");
        http.HttpContext = ServiceTestHelpers.ContextFor(requester.Id);
        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.FriendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = blocked.Id }));
    }
}
