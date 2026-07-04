using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Social.Client;
using Social.Friendships.Dto;
using Social.Tests.Infrastructure;

namespace Social.Tests.Controllers;

[Collection(SocialIntegrationTestCollection.Name)]
public sealed class FriendshipControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetMyFriends_ReturnsAccepted()
    {
        const string testMethodName = "FriendList";
        var me = CreateUserForTest(testMethodName, 1);
        var friend = CreateUserForTest(testMethodName, 2);
        var pending = CreateUserForTest(testMethodName, 3);

        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(me, friend);
        LoginAs(me);
        await SendFriendRequestAsync(pending);

        LoginAs(friend);
        await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        LoginAs(me);

        var res = await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        var only = Assert.Single(list);
        Assert.Equal(friend, only.OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsPublic()
    {
        const string testMethodName = "FriendUserListPublic";
        var owner = CreateUserForTest(testMethodName, 1);
        var friend = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        SetFriendsListVisibility(owner, FriendsListVisibility.Public);

        LoginAs(stranger);

        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(friend, Assert.Single(list).OtherUserId);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsFriendsOnlyAndViewerIsStranger()
    {
        const string testMethodName = "FriendUserListFriendsOnly";
        var owner = CreateUserForTest(testMethodName, 1);
        var friend = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        SetFriendsListVisibility(owner, FriendsListVisibility.FriendsOnly);

        LoginAs(stranger);

        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("You must be friends to view this user's friends list.", problem.Detail);
    }

    [Fact]
    public async Task GetUserFriends_Returns200_WhenVisibilityIsFriendsOnlyAndViewerIsFriend()
    {
        const string testMethodName = "FriendUserListFriendViewer";
        var owner = CreateUserForTest(testMethodName, 1);
        var friend = CreateUserForTest(testMethodName, 2);
        var other = CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(owner, friend);
        await AcceptFriendshipAsync(owner, other);
        SetFriendsListVisibility(owner, FriendsListVisibility.FriendsOnly);

        LoginAs(friend);

        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetUserFriends_Returns401_WhenVisibilityIsPrivate()
    {
        const string testMethodName = "FriendUserListPrivate";
        var owner = CreateUserForTest(testMethodName, 1);
        var friend = CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(owner, friend);
        SetFriendsListVisibility(owner, FriendsListVisibility.Private);

        LoginAs(friend);

        var res = await Client.GetAsync($"{FriendshipsBase}/users/{owner}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("This user's friends list is private.", problem.Detail);
    }

    [Fact]
    public async Task Remove_Returns204_AndDeletesFriendship()
    {
        const string testMethodName = "FriendRemove";
        var a = CreateUserForTest(testMethodName, 1);
        var b = CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(a, b);
        LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b)).Id;

        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);

        var friends = await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(friends, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Remove_Returns401_WhenStranger()
    {
        const string testMethodName = "FriendRemoveStranger";
        var a = CreateUserForTest(testMethodName, 1);
        var b = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(a, b);
        LoginAs(a);
        var friendshipId = (await GetAcceptedFriendAsync(b)).Id;

        LoginAs(stranger);

        var res = await Client.DeleteAsync($"{FriendshipsBase}/{friendshipId}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Unauthorized access", problem.Detail);
    }
}
