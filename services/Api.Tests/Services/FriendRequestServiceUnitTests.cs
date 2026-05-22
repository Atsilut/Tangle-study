using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendRequestServiceUnitTests
{
    private readonly FriendRequestService _friendRequestService;
    private readonly FriendshipService _friendshipService;
    private readonly UserBlockService _userBlockService;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeFriendRequestRepository _friendRequestRepository;
    private readonly FakeFriendshipRepository _friendshipRepository;
    private readonly FakeUserBlockRepository _userBlockRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public FriendRequestServiceUnitTests()
    {
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_httpContextAccessor);
        _friendRequestService = graph.FriendRequestService;
        _friendshipService = graph.FriendshipService;
        _userBlockService = graph.UserBlockService;
        _userRepository = graph.UserRepository;
        _friendRequestRepository = graph.FriendRequestRepository;
        _friendshipRepository = graph.FriendshipRepository;
        _userBlockRepository = graph.UserBlockRepository;
    }

    private async Task<User> CreateTestUserAsync(string nickname)
    {
        var user = new User(
            email: $"{nickname}@test.com",
            password: "password",
            nickname: nickname);
        await _userRepository.CreateUserAsync(user);
        return user;
    }

    private void LoginAs(long userId) =>
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
        };

    private async Task<long> SendFriendRequestAndGetIdAsync(long requesterId, long addresseeId)
    {
        LoginAs(requesterId);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        var pending = await _friendRequestService.GetPendingAsync();
        return pending!.Single(p => p.OtherUserId == addresseeId).Id;
    }

    // --- CREATE ---

    [Fact]
    public async Task SendRequest_CreatesPendingFriendRequest()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(requester.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        var pending = await _friendRequestService.GetPendingAsync();
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.Equal(requester.Id, dto.RequesterId);
        Assert.Equal(addressee.Id, dto.AddresseeId);
        Assert.Equal(addressee.Id, dto.OtherUserId);
        Assert.False(dto.IsIncoming);
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task SendRequest_ThrowsArgument_WhenAddresseeIsSelf()
    {
        // Arrange
        var user = await CreateTestUserAsync("solo");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = user.Id }));
    }

    [Fact]
    public async Task SendRequest_ThrowsNotFound_WhenAddresseeMissing()
    {
        // Arrange
        const long missingAddresseeId = 999;
        var requester = await CreateTestUserAsync("lonely");
        LoginAs(requester.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = missingAddresseeId }));
    }

    [Fact]
    public async Task SendRequest_CreatesFriendshipAndRemovesReversePending_WhenAddresseeSendsBack()
    {
        // Arrange
        var a = await CreateTestUserAsync("userA");
        var b = await CreateTestUserAsync("userB");
        var originalRequestId = await SendFriendRequestAndGetIdAsync(a.Id, b.Id);
        LoginAs(b.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = a.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest, outcome);
        Assert.NotNull(await _friendshipRepository.GetForUserPairAsync(a.Id, b.Id));
        Assert.Null(await _friendRequestRepository.GetByIdAsync(originalRequestId));
        Assert.Null(await _friendRequestRepository.GetForUserPairAsync(a.Id, b.Id));
        Assert.Null(await _friendRequestService.GetPendingAsync());
    }

    [Fact]
    public async Task SendRequest_CreatesFriendship_WhenAddresseeSendsAfterIgnoringIncoming()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);
        LoginAs(addressee.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest, outcome);
        Assert.NotNull(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
        Assert.Null(await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task SendRequest_ReturnsCreated_WhenDuplicateOutgoingRequest()
    {
        // Arrange
        var a = await CreateTestUserAsync("userA");
        var b = await CreateTestUserAsync("userB");
        await SendFriendRequestAndGetIdAsync(a.Id, b.Id);
        LoginAs(a.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = b.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
    }

    [Fact]
    public async Task SendRequest_StoresAtMostOneRowPerUserPair()
    {
        // Arrange
        var a = await CreateTestUserAsync("userA");
        var b = await CreateTestUserAsync("userB");
        LoginAs(a.Id);
        await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = b.Id });

        // Act
        var forA = await _friendRequestRepository.GetForUserAsync(a.Id);
        var forB = await _friendRequestRepository.GetForUserAsync(b.Id);

        // Assert
        Assert.Single(forA);
        Assert.Single(forB);
        Assert.Equal(forA[0].Id, forB[0].Id);
    }

    [Fact]
    public async Task SendRequest_ReturnsCreatedWithoutReactivate_WhenResendingAfterAddresseeIgnored()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);
        LoginAs(requester.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        Assert.False(await _userBlockRepository.ExistsAsync(addressee.Id, requester.Id));
        var pending = await _friendRequestService.GetPendingAsync();
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.Equal(addressee.Id, dto.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_ReturnsCreatedWithoutReactivate_WhenResendingAfterAddresseeIgnoredThenBlocked()
    {
        // Arrange — pending A→B, B ignores, B blocks A, A resends (same outcome as ignore-only resend)
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        LoginAs(requester.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        Assert.True(await _userBlockRepository.ExistsAsync(addressee.Id, requester.Id));
        var pending = await _friendRequestService.GetPendingAsync();
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.Equal(addressee.Id, dto.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_ThrowsArgument_WhenRequesterBlockedAddressee()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(requester.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });
        LoginAs(requester.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendRequestService.SendRequestAsync(
                new FriendRequestCreateRequestDto { AddresseeId = addressee.Id }));
    }

    [Fact]
    public async Task SendRequest_CreatesNonPendingRequest_WhenAddresseeBlockedRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(addressee.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        LoginAs(requester.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        var stored = await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        var pending = await _friendRequestService.GetPendingAsync();
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.Equal(addressee.Id, dto.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_ReturnsCreatedWithoutReactivate_WhenAddresseeBlockedRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        LoginAs(requester.Id);

        // Act
        var outcome = await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(SendFriendRequestOutcome.FriendRequestCreated, outcome);
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        Assert.True(await _userBlockRepository.ExistsAsync(addressee.Id, requester.Id));
    }

    // --- GET ---

    [Fact]
    public async Task GetPending_StillShowsOutgoingAsPending_WhenAddresseeIgnored()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);
        LoginAs(requester.Id);

        // Act
        var pending = await _friendRequestService.GetPendingAsync();

        // Assert
        var dto = Assert.Single(pending);
        Assert.True(dto.IsPending);
        Assert.False(dto.IsIncoming);
        Assert.Equal(addressee.Id, dto.OtherUserId);
    }

    [Fact]
    public async Task GetPending_ReturnsBothIncomingAndOutgoing()
    {
        // Arrange
        var me = await CreateTestUserAsync("me");
        var outgoing = await CreateTestUserAsync("outgoing");
        var incoming = await CreateTestUserAsync("incoming");
        LoginAs(me.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = outgoing.Id });
        LoginAs(incoming.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = me.Id });
        LoginAs(me.Id);

        // Act
        var pendings = await _friendRequestService.GetPendingAsync();

        // Assert
        Assert.Equal(2, pendings!.Count);
        Assert.Contains(pendings, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(pendings, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }

    // --- UPDATE ---

    [Fact]
    public async Task IgnoreRequest_IgnoresRequestWithoutUserBlock()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);

        // Act
        await _friendRequestService.IgnoreRequestAsync(requestId);

        // Assert
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        Assert.False(await _userBlockRepository.ExistsAsync(addressee.Id, requester.Id));
    }

    [Fact]
    public async Task Accept_CreatesFriendshipAndRemovesRequest_WhenCalledByAddressee()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);

        // Act
        await _friendRequestService.AcceptRequestAsync(requestId);

        // Assert
        Assert.NotNull(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
        Assert.Null(await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id));
        LoginAs(requester.Id);
        var friends = await _friendshipService.GetMyFriendsAsync();
        Assert.Equal(addressee.Id, Assert.Single(friends!).OtherUserId);
    }

    [Fact]
    public async Task Accept_ThrowsUnauthorized_WhenCalledByRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(requester.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenRequestAlreadyAccepted()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.AcceptRequestAsync(requestId);
        LoginAs(addressee.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenAddresseeBlockedRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        LoginAs(addressee.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task IgnoreRequest_NoOp_WhenAlreadyIgnoredByBlock()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });

        // Act
        await _friendRequestService.IgnoreRequestAsync(requestId);

        // Assert
        var stored = await _friendRequestRepository.GetByIdAsync(requestId);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
    }

    [Fact]
    public async Task Accept_ThrowsNotFound_WhenRequesterBlockedAddressee()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(requester.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });
        LoginAs(addressee.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
        Assert.Null(await _friendRequestRepository.GetByIdAsync(requestId));
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task Accept_ThrowsNotFound_WhenRequesterBlockedAddresseeThenUnblocked()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(requester.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });
        var blockId = Assert.Single((await _userBlockService.GetMyBlocksAsync())!).Id;
        await _userBlockService.DeleteBlockByIdAsync(blockId);
        LoginAs(addressee.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendRequestService.AcceptRequestAsync(requestId));
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    // --- DELETE ---

    [Fact]
    public async Task Reject_RemovesRequest_WhenCalledByAddressee()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);

        // Act
        await _friendRequestService.RejectRequestAsync(requestId);

        // Assert
        Assert.Null(await _friendRequestRepository.GetByIdAsync(requestId));
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task Reject_ThrowsUnauthorized_WhenCalledByRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(requester.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendRequestService.RejectRequestAsync(requestId));
    }

    [Fact]
    public async Task DeleteRequestById_RemovesPending_WhenCalledByRequester()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(requester.Id);

        // Act
        await _friendRequestService.DeleteRequestByIdAsync(requestId);

        // Assert
        Assert.Null(await _friendRequestRepository.GetByIdAsync(requestId));
        Assert.Null(await _friendshipRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task DeleteRequestById_RemovesPending_WhenCalledByAddressee()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);

        // Act
        await _friendRequestService.DeleteRequestByIdAsync(requestId);

        // Assert
        Assert.Null(await _friendRequestRepository.GetByIdAsync(requestId));
    }

    [Fact]
    public async Task DeleteRequestById_ThrowsArgument_WhenRequestNotPending()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        var requestId = await SendFriendRequestAndGetIdAsync(requester.Id, addressee.Id);
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);
        LoginAs(addressee.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _friendRequestService.DeleteRequestByIdAsync(requestId));
        Assert.NotNull(await _friendRequestRepository.GetByIdAsync(requestId));
    }
}
