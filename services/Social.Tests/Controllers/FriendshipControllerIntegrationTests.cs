using System.Net;
using System.Net.Http.Json;
using Social.Client;
using Social.Friendships.Dto;
using Social.Tests.Infrastructure;
using Social.UserBlocks.Dto;

namespace Social.Tests.Controllers;

[Collection(SocialIntegrationTestCollection.Name)]
public sealed class FriendshipControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private const string FriendshipsBase = "/api/friendships";
    private const string RequestsBase = "/api/friendships/requests";
    private const string BlocksBase = "/api/users/blocks";

    [Fact]
    public async Task SendRequest_AndAccept_CreatesFriendship()
    {
        var requester = MonolithAccess.SeedUser("requester");
        var addressee = MonolithAccess.SeedUser("addressee");

        SocialTestAuthHelpers.LoginAs(Client, requester);
        var send = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);

        SocialTestAuthHelpers.LoginAs(Client, addressee);
        var pending = await Client.GetFromJsonAsync<List<FriendRequestGetResponseDto>>(
            $"{RequestsBase}/pending",
            TestContext.Current.CancellationToken);
        Assert.NotNull(pending);
        var requestId = pending.Single(r => r.IsIncoming).Id;

        var accept = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        SocialTestAuthHelpers.LoginAs(Client, requester);
        var friends = await Client.GetFromJsonAsync<List<FriendshipGetResponseDto>>(
            $"{FriendshipsBase}/me",
            TestContext.Current.CancellationToken);
        Assert.NotNull(friends);
        Assert.Contains(friends, f => f.OtherUserId == addressee);
    }

    [Fact]
    public async Task Block_IgnoresIncomingPendingRequest()
    {
        var requester = MonolithAccess.SeedUser("requester");
        var addressee = MonolithAccess.SeedUser("addressee");

        SocialTestAuthHelpers.LoginAs(Client, requester);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        SocialTestAuthHelpers.LoginAs(Client, addressee);
        var block = await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = requester },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var pending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);

        var ignored = await Client.GetFromJsonAsync<List<FriendRequestGetResponseDto>>(
            $"{RequestsBase}/ignored",
            TestContext.Current.CancellationToken);
        Assert.NotNull(ignored);
        Assert.Single(ignored);
    }

    [Fact]
    public async Task Internal_ValidateFriendshipPair_Returns204_WhenFriends()
    {
        var a = MonolithAccess.SeedUser("a");
        var b = MonolithAccess.SeedUser("b");

        SocialTestAuthHelpers.LoginAs(Client, a);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = b },
            TestContext.Current.CancellationToken);
        SocialTestAuthHelpers.LoginAs(Client, b);
        var pending = await Client.GetFromJsonAsync<List<FriendRequestGetResponseDto>>(
            $"{RequestsBase}/pending",
            TestContext.Current.CancellationToken);
        await Client.PostAsync(
            $"{RequestsBase}/{pending!.Single().Id}/accept",
            null,
            TestContext.Current.CancellationToken);

        SocialTestAuthHelpers.LoginAsInternal(Client, a);
        var res = await Client.PostAsJsonAsync(
            "/internal/social/friendships/validate-pair",
            new { OtherUserId = b },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Internal_DetachOnDeletion_RemovesSocialRows()
    {
        var a = MonolithAccess.SeedUser("a");
        var b = MonolithAccess.SeedUser("b");

        SocialTestAuthHelpers.LoginAs(Client, a);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = b },
            TestContext.Current.CancellationToken);
        await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = b },
            TestContext.Current.CancellationToken);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var detach = await Client.PostAsync(
            $"/internal/social/users/{a}/detach-on-deletion",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);

        SocialTestAuthHelpers.LoginAs(Client, a);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GetUserFriends_RespectsPublicVisibility()
    {
        var owner = MonolithAccess.SeedUser("owner", visibility: FriendsListVisibility.Public);
        var friend = MonolithAccess.SeedUser("friend");
        var viewer = MonolithAccess.SeedUser("viewer");

        SocialTestAuthHelpers.LoginAs(Client, owner);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = friend },
            TestContext.Current.CancellationToken);
        SocialTestAuthHelpers.LoginAs(Client, friend);
        var pending = await Client.GetFromJsonAsync<List<FriendRequestGetResponseDto>>(
            $"{RequestsBase}/pending",
            TestContext.Current.CancellationToken);
        await Client.PostAsync(
            $"{RequestsBase}/{pending!.Single().Id}/accept",
            null,
            TestContext.Current.CancellationToken);

        SocialTestAuthHelpers.LoginAs(Client, viewer);
        var friends = await Client.GetFromJsonAsync<List<FriendshipGetResponseDto>>(
            $"{FriendshipsBase}/users/{owner}",
            TestContext.Current.CancellationToken);
        Assert.NotNull(friends);
        Assert.Contains(friends, f => f.OtherUserId == friend);
    }
}
