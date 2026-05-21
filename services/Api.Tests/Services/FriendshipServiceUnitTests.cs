using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly FriendshipService _friendshipService;
    private readonly FakeFriendshipRepository _friendshipRepository;
    private readonly FakeUserRepository _userRepository;

    public FriendshipServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _friendshipService = graph.FriendshipService;
        _friendshipRepository = graph.FriendshipRepository;
        _userRepository = graph.UserRepository;
    }

    private async Task<User> CreateUserAsync(string nickname)
    {
        var user = new User($"{nickname}@test.com", "password", nickname);
        await _userRepository.CreateUserAsync(user);
        return user;
    }

    private void LoginAs(long userId) =>
        _http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    // --- SEND ---

    [Fact]
    public async Task SendRequest_CreatesPendingFriendship()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);

        // Act
        var dto = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(FriendshipStatus.Pending, dto.Status);
        Assert.Equal(requester.Id, dto.RequesterId);
        Assert.Equal(addressee.Id, dto.AddresseeId);
        Assert.Equal(addressee.Id, dto.OtherUserId);
        Assert.False(dto.IsIncoming);
    }

    [Fact]
    public async Task SendRequest_ThrowsArgument_WhenAddresseeIsSelf()
    {
        // Arrange
        var user = await CreateUserAsync("solo");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = user.Id }));
    }

    [Fact]
    public async Task SendRequest_ThrowsNotFound_WhenAddresseeMissing()
    {
        // Arrange
        var requester = await CreateUserAsync("lonely");
        LoginAs(requester.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = 999 }));
    }

    [Fact]
    public async Task SendRequest_ThrowsAlreadyExists_WhenFriendshipExistsInReverseDirection()
    {
        // Arrange
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        LoginAs(a.Id);
        await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = b.Id });

        // Act & Assert
        LoginAs(b.Id);
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = a.Id }));
    }

    // --- ACCEPT ---

    [Fact]
    public async Task Accept_TransitionsPendingToAccepted_WhenCalledByAddressee()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });

        // Act
        LoginAs(addressee.Id);
        var accepted = await _friendshipService.AcceptRequestAsync(created.Id);

        // Assert
        Assert.Equal(FriendshipStatus.Accepted, accepted.Status);
        Assert.True(accepted.IsIncoming);
    }

    [Fact]
    public async Task Accept_ThrowsUnauthorized_WhenCalledByRequester()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.AcceptRequestAsync(created.Id));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenAlreadyAccepted()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });
        LoginAs(addressee.Id);
        await _friendshipService.AcceptRequestAsync(created.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendshipService.AcceptRequestAsync(created.Id));
    }

    // --- REJECT ---

    [Fact]
    public async Task Reject_TransitionsPendingToRejected_WhenCalledByAddressee()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });

        // Act
        LoginAs(addressee.Id);
        var rejected = await _friendshipService.RejectRequestAsync(created.Id);

        // Assert
        Assert.Equal(FriendshipStatus.Rejected, rejected.Status);
    }

    [Fact]
    public async Task Reject_ThrowsUnauthorized_WhenCalledByRequester()
    {
        // Arrange
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = addressee.Id });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.RejectRequestAsync(created.Id));
    }

    // --- REMOVE ---

    [Fact]
    public async Task Remove_DeletesFriendship_WhenCalledByEitherParty()
    {
        // Arrange
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        LoginAs(a.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = b.Id });

        // Act
        LoginAs(b.Id);
        await _friendshipService.RemoveAsync(created.Id);

        // Assert
        Assert.Null(await _friendshipRepository.GetFriendshipByIdAsync(created.Id));
    }

    [Fact]
    public async Task Remove_ThrowsUnauthorized_WhenUserNotPartOfFriendship()
    {
        // Arrange
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        var stranger = await CreateUserAsync("stranger");
        LoginAs(a.Id);
        var created = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = b.Id });

        // Act & Assert
        LoginAs(stranger.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.RemoveAsync(created.Id));
    }

    [Fact]
    public async Task Remove_ThrowsNotFound_WhenMissing()
    {
        // Arrange
        var user = await CreateUserAsync("user");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendshipService.RemoveAsync(999));
    }

    // --- QUERIES ---

    [Fact]
    public async Task GetMyFriends_ReturnsOnlyAccepted()
    {
        // Arrange
        var me = await CreateUserAsync("me");
        var friend = await CreateUserAsync("friend");
        var pending = await CreateUserAsync("pending");
        LoginAs(me.Id);
        var accepted = await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = friend.Id });
        await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = pending.Id });
        LoginAs(friend.Id);
        await _friendshipService.AcceptRequestAsync(accepted.Id);

        // Act
        LoginAs(me.Id);
        var friends = await _friendshipService.GetMyFriendsAsync();

        // Assert
        var single = Assert.Single(friends);
        Assert.Equal(friend.Id, single.OtherUserId);
        Assert.Equal(FriendshipStatus.Accepted, single.Status);
    }

    [Fact]
    public async Task GetPending_ReturnsBothIncomingAndOutgoing()
    {
        // Arrange
        var me = await CreateUserAsync("me");
        var outgoing = await CreateUserAsync("outgoing");
        var incoming = await CreateUserAsync("incoming");
        LoginAs(me.Id);
        await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = outgoing.Id });
        LoginAs(incoming.Id);
        await _friendshipService.SendRequestAsync(new FriendshipCreateRequestDto { AddresseeId = me.Id });

        // Act
        LoginAs(me.Id);
        var pendings = await _friendshipService.GetPendingAsync();

        // Assert
        Assert.Equal(2, pendings.Count);
        Assert.Contains(pendings, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(pendings, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }
}
