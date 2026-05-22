using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendRequestServiceUnitTests
{
    private readonly FakeHttpContextAccessor _http;
    private readonly FriendRequestService _friendRequestService;
    private readonly FriendshipService _friendshipService;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeFriendRequestRepository _friendRequestRepository;
    private readonly FakeFriendshipRepository _friendshipRepository;

    public FriendRequestServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _friendRequestService = graph.FriendRequestService;
        _friendshipService = graph.FriendshipService;
        _userRepository = graph.UserRepository;
        _friendRequestRepository = graph.FriendRequestRepository;
        _friendshipRepository = graph.FriendshipRepository;
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

    private async Task<long> SendRequestAndGetIdAsync(long requesterId, long addresseeId)
    {
        LoginAs(requesterId);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        var pending = await _friendRequestService.GetPendingAsync();
        return pending!.Single(p => p.OtherUserId == addresseeId).Id;
    }

    [Fact]
    public async Task SendRequest_CreatesPendingFriendRequest()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        LoginAs(requester.Id);

        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);

        var pending = await _friendRequestService.GetPendingAsync();
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.Equal(requester.Id, dto.RequesterId);
        Assert.Equal(addressee.Id, dto.AddresseeId);
        Assert.Equal(addressee.Id, dto.OtherUserId);
        Assert.False(dto.IsIncoming);
        Assert.Null(await _friendshipRepository.GetBetweenAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task SendRequest_ThrowsArgument_WhenAddresseeIsSelf()
    {
        var user = await CreateUserAsync("solo");
        LoginAs(user.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = user.Id }));
    }

    [Fact]
    public async Task SendRequest_ThrowsNotFound_WhenAddresseeMissing()
    {
        var requester = await CreateUserAsync("lonely");
        LoginAs(requester.Id);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = 999 }));
    }

    [Fact]
    public async Task SendRequest_CreatesFriendshipAndRemovesReversePending_WhenAddresseeSendsBack()
    {
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        var originalRequestId = await SendRequestAndGetIdAsync(a.Id, b.Id);

        LoginAs(b.Id);
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = a.Id });

        Assert.Equal(SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest, outcome);
        Assert.NotNull(await _friendshipRepository.GetBetweenAsync(a.Id, b.Id));
        Assert.Null(await _friendRequestRepository.GetByIdAsync(originalRequestId));
        Assert.Null(await _friendRequestRepository.GetBetweenAsync(a.Id, b.Id));
        Assert.Null(await _friendRequestService.GetPendingAsync());
    }

    [Fact]
    public async Task SendRequest_ThrowsAlreadyExists_WhenDuplicateOutgoingRequest()
    {
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        await SendRequestAndGetIdAsync(a.Id, b.Id);

        LoginAs(a.Id);
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = b.Id }));
    }

    [Fact]
    public async Task GetPending_ReturnsBothIncomingAndOutgoing()
    {
        var me = await CreateUserAsync("me");
        var outgoing = await CreateUserAsync("outgoing");
        var incoming = await CreateUserAsync("incoming");
        LoginAs(me.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = outgoing.Id });
        LoginAs(incoming.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = me.Id });

        LoginAs(me.Id);
        var pendings = await _friendRequestService.GetPendingAsync();

        Assert.Equal(2, pendings!.Count);
        Assert.Contains(pendings, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(pendings, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }

    [Fact]
    public async Task Accept_CreatesFriendshipAndRemovesRequest_WhenCalledByAddressee()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        var requestId = await SendRequestAndGetIdAsync(requester.Id, addressee.Id);

        LoginAs(addressee.Id);
        await _friendRequestService.AcceptRequestAsync(requestId);

        Assert.NotNull(await _friendshipRepository.GetBetweenAsync(requester.Id, addressee.Id));
        Assert.Null(await _friendRequestRepository.GetBetweenAsync(requester.Id, addressee.Id));
        LoginAs(requester.Id);
        var friends = await _friendshipService.GetMyFriendsAsync();
        Assert.Equal(addressee.Id, Assert.Single(friends!).OtherUserId);
    }

    [Fact]
    public async Task Accept_ThrowsUnauthorized_WhenCalledByRequester()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        var requestId = await SendRequestAndGetIdAsync(requester.Id, addressee.Id);

        LoginAs(requester.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenRequestAlreadyAccepted()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        var requestId = await SendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.AcceptRequestAsync(requestId);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
    }

    [Fact]
    public async Task Reject_RemovesRequest_WhenCalledByAddressee()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        var requestId = await SendRequestAndGetIdAsync(requester.Id, addressee.Id);

        LoginAs(addressee.Id);
        await _friendRequestService.RejectRequestAsync(requestId);

        Assert.Null(await _friendRequestRepository.GetByIdAsync(requestId));
        Assert.Null(await _friendshipRepository.GetBetweenAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task Reject_ThrowsUnauthorized_WhenCalledByRequester()
    {
        var requester = await CreateUserAsync("requester");
        var addressee = await CreateUserAsync("addressee");
        var requestId = await SendRequestAndGetIdAsync(requester.Id, addressee.Id);

        LoginAs(requester.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendRequestService.RejectRequestAsync(requestId));
    }
}
