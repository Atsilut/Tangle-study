using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendRequestServiceUnitTests
{
    [Fact]
    public async Task SendRequest_CreatesPendingRequest()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var requester = await CreateUserAsync(graph.UserRepository, "a");
        var addressee = await CreateUserAsync(graph.UserRepository, "b");
        http.HttpContext = ContextFor(requester.Id);

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
        var user = await CreateUserAsync(graph.UserRepository, "solo");
        http.HttpContext = ContextFor(user.Id);

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
        var requester = await CreateUserAsync(graph.UserRepository, "blocker");
        var blocked = await CreateUserAsync(graph.UserRepository, "blocked");
        http.HttpContext = ContextFor(requester.Id);
        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.FriendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = blocked.Id }));
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
