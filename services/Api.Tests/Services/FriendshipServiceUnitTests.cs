using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class FriendshipServiceUnitTests
{
    private readonly FriendshipService _friendshipService;
    private readonly FriendRequestService _friendRequestService;
    private readonly UserService _userService;
    private readonly FakeFriendshipRepository _friendshipRepository;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;

    public FriendshipServiceUnitTests()
    {
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_httpContextAccessor);
        _friendshipService = graph.FriendshipService;
        _friendRequestService = graph.FriendRequestService;
        _userService = graph.UserService;
        _friendshipRepository = graph.FriendshipRepository;
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

    private async Task SetFriendsListVisibilityAsync(long userId, FriendsListVisibility visibility)
    {
        LoginAs(userId);
        await _userService.UpdatePrivacySettingsAsync(
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = visibility });
    }

    private async Task AcceptFriendshipAsync(long requesterId, long addresseeId)
    {
        LoginAs(requesterId);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        LoginAs(addresseeId);
        var pending = await _friendRequestService.GetPendingAsync();
        var requestId = pending!.Single(p => p.OtherUserId == requesterId).Id;
        await _friendRequestService.AcceptRequestAsync(requestId);
    }

    // --- GET ---

    [Fact]
    public async Task GetMyFriends_ReturnsOnlyAccepted()
    {
        // Arrange
        var me = await CreateTestUserAsync("me");
        var friend = await CreateTestUserAsync("friend");
        var pendingUser = await CreateTestUserAsync("pending");
        LoginAs(me.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = friend.Id });
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = pendingUser.Id });
        LoginAs(friend.Id);
        var incoming = await _friendRequestService.GetPendingAsync();
        await _friendRequestService.AcceptRequestAsync(incoming!.Single(p => p.OtherUserId == me.Id).Id);
        LoginAs(me.Id);

        // Act
        var friends = await _friendshipService.GetMyFriendsAsync();

        // Assert
        var single = Assert.Single(friends);
        Assert.Equal(friend.Id, single.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_ReturnsFriends_WhenVisibilityIsPublic()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var friend = await CreateTestUserAsync("friend");
        var stranger = await CreateTestUserAsync("stranger");
        await AcceptFriendshipAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.Public);
        LoginAs(stranger.Id);

        // Act
        var friends = await _friendshipService.GetUserFriendsAsync(owner.Id);

        // Assert
        var single = Assert.Single(friends);
        Assert.Equal(friend.Id, single.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_ThrowsUnauthorized_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var friend = await CreateTestUserAsync("friend");
        var stranger = await CreateTestUserAsync("stranger");
        await AcceptFriendshipAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.FriendsOnly);
        LoginAs(stranger.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.GetUserFriendsAsync(owner.Id));
    }

    [Fact]
    public async Task GetUserFriends_ReturnsFriends_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var friend = await CreateTestUserAsync("friend");
        var other = await CreateTestUserAsync("other");
        await AcceptFriendshipAsync(owner.Id, friend.Id);
        await AcceptFriendshipAsync(owner.Id, other.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.FriendsOnly);
        LoginAs(friend.Id);

        // Act
        var friends = await _friendshipService.GetUserFriendsAsync(owner.Id);

        // Assert
        Assert.Equal(2, friends!.Count);
        Assert.Contains(friends, f => f.OtherUserId == other.Id);
    }

    [Fact]
    public async Task GetUserFriends_ThrowsUnauthorized_WhenVisibilityIsPrivateAndViewerIsFriend()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner");
        var friend = await CreateTestUserAsync("friend");
        await AcceptFriendshipAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.Private);
        LoginAs(friend.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.GetUserFriendsAsync(owner.Id));
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteFriendship_RemovesFriendship_WhenCalledByEitherParty()
    {
        // Arrange
        var a = await CreateTestUserAsync("userA");
        var b = await CreateTestUserAsync("userB");
        await AcceptFriendshipAsync(a.Id, b.Id);
        LoginAs(a.Id);
        var friendshipId = Assert.Single((await _friendshipService.GetMyFriendsAsync())!).Id;
        LoginAs(b.Id);

        // Act
        await _friendshipService.DeleteFriendshipByIdAsync(friendshipId);

        // Assert
        Assert.Null(await _friendshipRepository.GetByIdAsync(friendshipId));
    }

    [Fact]
    public async Task DeleteFriendship_ThrowsUnauthorized_WhenUserNotPartOfFriendship()
    {
        // Arrange
        var a = await CreateTestUserAsync("userA");
        var b = await CreateTestUserAsync("userB");
        var stranger = await CreateTestUserAsync("stranger");
        await AcceptFriendshipAsync(a.Id, b.Id);
        LoginAs(a.Id);
        var friendshipId = Assert.Single((await _friendshipService.GetMyFriendsAsync())!).Id;
        LoginAs(stranger.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.DeleteFriendshipByIdAsync(friendshipId));
    }

    [Fact]
    public async Task DeleteFriendship_ThrowsNotFound_WhenMissing()
    {
        // Arrange
        const long missingFriendshipId = 999;
        var user = await CreateTestUserAsync("user");
        LoginAs(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendshipService.DeleteFriendshipByIdAsync(missingFriendshipId));
    }
}
