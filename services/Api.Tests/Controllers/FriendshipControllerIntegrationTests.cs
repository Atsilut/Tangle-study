using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FriendshipControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    // --- GET ---

    [Fact]
    public async Task GetMyFriends_ReturnsAccepted()
    {
        // Arrange
        const string testMethodName = "FriendList";
        var me = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var pending = await CreateUserForTest(testMethodName, 3);

        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(me, friend);
        await LoginAs(me);
        await SendFriendRequestAsync(pending.Id);

        await LoginAs(friend);
        await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        await LoginAs(me);

        // Act
        var res = await Client.GetAsync($"{FriendshipsBase}/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.NotNull(list);
        var only = Assert.Single(list);
        Assert.Equal(friend.Id, only.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsPublic()
    {
        // Arrange
        const string testMethodName = "FriendUserListPublic";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Public);

        await LoginAs(stranger);

        // Act
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.NotNull(list);
        Assert.Equal(friend.Id, Assert.Single(list).OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        // Arrange
        const string testMethodName = "FriendUserListFriendsOnly";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(stranger);

        // Act
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        // Arrange
        const string testMethodName = "FriendUserListFriendViewer";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var other = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await AcceptFriendshipAsync(owner, other);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(friend);

        // Act
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsPrivate()
    {
        // Arrange
        const string testMethodName = "FriendUserListPrivate";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Private);

        await LoginAs(friend);

        // Act
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- DELETE ---

    [Fact]
    public async Task Remove_Returns204_AndDeletesFriendship()
    {
        // Arrange
        const string testMethodName = "FriendRemove";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(a, b);
        await LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b.Id)).Id;

        // Act
        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var friends = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.NoContent, friends.StatusCode);
    }

    [Fact]
    public async Task Remove_Returns401_WhenStranger()
    {
        // Arrange
        const string testMethodName = "FriendRemoveStranger";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(a, b);
        await LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b.Id)).Id;

        await LoginAs(stranger);

        // Act
        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
