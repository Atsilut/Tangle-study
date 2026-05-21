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
    [Fact]
    public async Task GetMyFriends_ReturnsAccepted()
    {
        const string testMethodName = "FriendList";
        var me = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var pendingUser = await CreateUserForTest(testMethodName, 3);

        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(me, friend);
        await LoginAs(me);
        await SendFriendRequestAsync(pendingUser.Id);

        await LoginAs(friend);
        await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        await LoginAs(me);
        var res = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        var only = Assert.Single(list!);
        Assert.Equal(friend.Id, only.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsPublic()
    {
        const string testMethodName = "FriendUserListPublic";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Public);

        await LoginAs(stranger);
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.Equal(friend.Id, Assert.Single(list!).OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        const string testMethodName = "FriendUserListFriendsOnly";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(stranger);
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        const string testMethodName = "FriendUserListFriendViewer";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        var other = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await AcceptFriendshipAsync(owner, other);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.FriendsOnly);

        await LoginAs(friend);
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipResponseDto>>();
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsPrivate()
    {
        const string testMethodName = "FriendUserListPrivate";
        var owner = await CreateUserForTest(testMethodName, 1);
        var friend = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(owner, friend);
        await SetFriendsListVisibilityAsync(owner, FriendsListVisibility.Private);

        await LoginAs(friend);
        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Remove_Returns204_AndDeletesFriendship()
    {
        const string testMethodName = "FriendRemove";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(a, b);
        await LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b.Id)).Id;

        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var friends = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.NoContent, friends.StatusCode);
    }

    [Fact]
    public async Task Remove_Returns401_WhenStranger()
    {
        const string testMethodName = "FriendRemoveStranger";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(a, b);
        await LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b.Id)).Id;

        await LoginAs(stranger);
        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
