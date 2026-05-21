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
    private readonly FakeHttpContextAccessor _http;
    private readonly FriendshipService _friendshipService;
    private readonly FriendRequestService _friendRequestService;
    private readonly UserService _userService;
    private readonly FakeFriendshipRepository _friendshipRepository;
    private readonly FakeUserRepository _userRepository;

    public FriendshipServiceUnitTests()
    {
        _http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(_http);
        _friendshipService = graph.FriendshipService;
        _friendRequestService = graph.FriendRequestService;
        _userService = graph.UserService;
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

    private async Task SetFriendsListVisibilityAsync(long userId, FriendsListVisibility visibility)
    {
        LoginAs(userId);
        await _userService.UpdatePrivacySettingsAsync(
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = visibility });
    }

    private async Task AcceptFriendshipBetweenAsync(long requesterId, long addresseeId)
    {
        LoginAs(requesterId);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        LoginAs(addresseeId);
        var pending = await _friendRequestService.GetPendingAsync();
        var requestId = pending!.Single(p => p.OtherUserId == requesterId).Id;
        await _friendRequestService.AcceptRequestAsync(requestId);
    }

    [Fact]
    public async Task GetMyFriends_ReturnsOnlyAccepted()
    {
        var me = await CreateUserAsync("me");
        var friend = await CreateUserAsync("friend");
        var pending = await CreateUserAsync("pending");
        LoginAs(me.Id);
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = friend.Id });
        await _friendRequestService.SendRequestAsync(new FriendRequestCreateRequestDto { AddresseeId = pending.Id });
        LoginAs(friend.Id);
        var incoming = await _friendRequestService.GetPendingAsync();
        await _friendRequestService.AcceptRequestAsync(incoming!.Single(p => p.OtherUserId == me.Id).Id);

        LoginAs(me.Id);
        var friends = await _friendshipService.GetMyFriendsAsync();

        var single = Assert.Single(friends);
        Assert.Equal(friend.Id, single.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_ReturnsFriends_WhenVisibilityIsPublic()
    {
        var owner = await CreateUserAsync("owner");
        var friend = await CreateUserAsync("friend");
        var stranger = await CreateUserAsync("stranger");
        await AcceptFriendshipBetweenAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.Public);

        LoginAs(stranger.Id);
        var friends = await _friendshipService.GetUserFriendsAsync(owner.Id);

        var single = Assert.Single(friends);
        Assert.Equal(friend.Id, single.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_ThrowsUnauthorized_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        var owner = await CreateUserAsync("owner");
        var friend = await CreateUserAsync("friend");
        var stranger = await CreateUserAsync("stranger");
        await AcceptFriendshipBetweenAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.FriendsOnly);

        LoginAs(stranger.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.GetUserFriendsAsync(owner.Id));
    }

    [Fact]
    public async Task GetUserFriends_ReturnsFriends_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        var owner = await CreateUserAsync("owner");
        var friend = await CreateUserAsync("friend");
        var other = await CreateUserAsync("other");
        await AcceptFriendshipBetweenAsync(owner.Id, friend.Id);
        await AcceptFriendshipBetweenAsync(owner.Id, other.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.FriendsOnly);

        LoginAs(friend.Id);
        var friends = await _friendshipService.GetUserFriendsAsync(owner.Id);

        Assert.Equal(2, friends!.Count);
        Assert.Contains(friends, f => f.OtherUserId == other.Id);
    }

    [Fact]
    public async Task GetUserFriends_ThrowsUnauthorized_WhenVisibilityIsPrivateAndViewerIsFriend()
    {
        var owner = await CreateUserAsync("owner");
        var friend = await CreateUserAsync("friend");
        await AcceptFriendshipBetweenAsync(owner.Id, friend.Id);
        await SetFriendsListVisibilityAsync(owner.Id, FriendsListVisibility.Private);

        LoginAs(friend.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.GetUserFriendsAsync(owner.Id));
    }

    [Fact]
    public async Task DeleteFriendship_RemovesFriendship_WhenCalledByEitherParty()
    {
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        await AcceptFriendshipBetweenAsync(a.Id, b.Id);
        LoginAs(a.Id);
        var friends = await _friendshipService.GetMyFriendsAsync();
        var friendshipId = Assert.Single(friends!).Id;

        LoginAs(b.Id);
        await _friendshipService.DeleteFriendshipByIdAsync(friendshipId);

        Assert.Null(await _friendshipRepository.GetByIdAsync(friendshipId));
    }

    [Fact]
    public async Task DeleteFriendship_ThrowsUnauthorized_WhenUserNotPartOfFriendship()
    {
        var a = await CreateUserAsync("userA");
        var b = await CreateUserAsync("userB");
        var stranger = await CreateUserAsync("stranger");
        await AcceptFriendshipBetweenAsync(a.Id, b.Id);
        LoginAs(a.Id);
        var friendshipId = Assert.Single((await _friendshipService.GetMyFriendsAsync())!).Id;

        LoginAs(stranger.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _friendshipService.DeleteFriendshipByIdAsync(friendshipId));
    }

    [Fact]
    public async Task DeleteFriendship_ThrowsNotFound_WhenMissing()
    {
        var user = await CreateUserAsync("user");
        LoginAs(user.Id);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _friendshipService.DeleteFriendshipByIdAsync(999));
    }
}
