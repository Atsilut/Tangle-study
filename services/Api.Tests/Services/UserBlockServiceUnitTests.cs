using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class UserBlockServiceUnitTests
{
    private readonly UserBlockService _userBlockService;
    private readonly FriendRequestService _friendRequestService;
    private readonly FakeUserBlockRepository _userBlockRepository;
    private readonly FakeFriendRequestRepository _friendRequestRepository;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public UserBlockServiceUnitTests()
    {
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_httpContextAccessor);
        _userBlockService = graph.UserBlockService;
        _friendRequestService = graph.FriendRequestService;
        _userBlockRepository = graph.UserBlockRepository;
        _friendRequestRepository = graph.FriendRequestRepository;
        _userRepository = graph.UserRepository;
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

    // --- CREATE ---

    [Fact]
    public async Task BlockUser_CreatesBlock()
    {
        // Arrange
        var blocker = await CreateTestUserAsync("blocker");
        var blocked = await CreateTestUserAsync("blocked");
        LoginAs(blocker.Id);

        // Act
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Assert
        Assert.True(await _userBlockRepository.ExistsAsync(blocker.Id, blocked.Id));
        var blocks = await _userBlockService.GetMyBlocksAsync();
        var single = Assert.Single(blocks);
        Assert.Equal(blocked.Id, single.BlockedUserId);
        Assert.Equal("blocked", single.BlockedUserNickname);
    }

    [Fact]
    public async Task BlockUser_ThrowsArgument_WhenBlockingSelf()
    {
        // Arrange
        var user = await CreateTestUserAsync("solo");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = user.Id }));
    }

    [Fact]
    public async Task BlockUser_ThrowsNotFound_WhenBlockedUserMissing()
    {
        // Arrange
        const long missingUserId = 999;
        var blocker = await CreateTestUserAsync("blocker");
        LoginAs(blocker.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = missingUserId }));
        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task BlockUser_IgnoresPendingIncomingFriendRequest()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(requester.Id);
        await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Act
        LoginAs(addressee.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });

        // Assert
        var stored = await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsPending);
    }

    [Fact]
    public async Task BlockUser_DeletesPendingOutgoingFriendRequest()
    {
        // Arrange
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(requester.Id);
        await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Act
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });

        // Assert
        Assert.Null(await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task BlockUser_DeletesIgnoredFriendRequest_WhenRequesterBlocksAddressee()
    {
        // Arrange — pending A→B, B ignores, then A blocks B
        var requester = await CreateTestUserAsync("requester");
        var addressee = await CreateTestUserAsync("addressee");
        LoginAs(requester.Id);
        await _friendRequestService.SendRequestAsync(
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        var requestId = (await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id))!.Id;
        LoginAs(addressee.Id);
        await _friendRequestService.IgnoreRequestAsync(requestId);

        // Act
        LoginAs(requester.Id);
        await _userBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });

        // Assert
        Assert.Null(await _friendRequestRepository.GetForUserPairAsync(requester.Id, addressee.Id));
    }

    [Fact]
    public async Task BlockUser_ThrowsEntityAlreadyExists_WhenAlreadyBlocked()
    {
        // Arrange
        var blocker = await CreateTestUserAsync("blocker");
        var blocked = await CreateTestUserAsync("blocked");
        LoginAs(blocker.Id);
        var request = new UserBlockCreateRequestDto { BlockedUserId = blocked.Id };
        await _userBlockService.BlockUserAsync(request);

        // Act & Assert
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            _userBlockService.BlockUserAsync(request));
        Assert.Single(await _userBlockRepository.GetAllForBlockerAsync(blocker.Id));
    }

    // --- GET ---

    [Fact]
    public async Task GetMyBlocks_ReturnsNull_WhenEmpty()
    {
        // Arrange
        var user = await CreateTestUserAsync("lonely");
        LoginAs(user.Id);

        // Act
        var blocks = await _userBlockService.GetMyBlocksAsync();

        // Assert
        Assert.Null(blocks);
    }

    [Fact]
    public async Task GetMyBlocks_ReturnsOnlyCurrentUserBlocks()
    {
        // Arrange
        var blockerA = await CreateTestUserAsync("blockerA");
        var blockedB = await CreateTestUserAsync("blockedB");
        var blockerC = await CreateTestUserAsync("blockerC");
        var blockedD = await CreateTestUserAsync("blockedD");

        LoginAs(blockerA.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blockedB.Id });
        LoginAs(blockerC.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blockedD.Id });
        LoginAs(blockerA.Id);

        // Act
        var blocks = await _userBlockService.GetMyBlocksAsync();

        // Assert
        var single = Assert.Single(blocks);
        Assert.Equal(blockedB.Id, single.BlockedUserId);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteBlock_RemovesBlock_WhenCalledByBlocker()
    {
        // Arrange
        var blocker = await CreateTestUserAsync("blocker");
        var blocked = await CreateTestUserAsync("blocked");
        LoginAs(blocker.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });
        var blockId = Assert.Single((await _userBlockService.GetMyBlocksAsync())!).Id;

        // Act
        await _userBlockService.DeleteBlockByIdAsync(blockId);

        // Assert
        Assert.Null(await _userBlockRepository.GetByIdAsync(blockId));
        Assert.False(await _userBlockRepository.ExistsAsync(blocker.Id, blocked.Id));
        Assert.Null(await _userBlockService.GetMyBlocksAsync());
    }

    [Fact]
    public async Task DeleteBlock_ThrowsUnauthorized_WhenUserNotBlocker()
    {
        // Arrange
        var blocker = await CreateTestUserAsync("blocker");
        var blocked = await CreateTestUserAsync("blocked");
        var stranger = await CreateTestUserAsync("stranger");
        LoginAs(blocker.Id);
        await _userBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });
        var blockId = Assert.Single((await _userBlockService.GetMyBlocksAsync())!).Id;
        LoginAs(stranger.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _userBlockService.DeleteBlockByIdAsync(blockId));
    }

    [Fact]
    public async Task DeleteBlock_ThrowsNotFound_WhenMissing()
    {
        // Arrange
        const long missingBlockId = 999;
        var user = await CreateTestUserAsync("user");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _userBlockService.DeleteBlockByIdAsync(missingBlockId));
    }
}
