using System.Net;
using System.Net.Http.Json;
using Social.Api;
using Social.Tests.Infrastructure;

namespace Social.Tests.Controllers;

[Collection(SocialIntegrationTestCollection.Name)]
public sealed class InternalSocialControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    private const string InternalBase = "/internal/social";

    [Fact]
    public async Task ValidateFriendshipPair_Returns204_WhenFriends()
    {
        var a = CreateUserForTest("InternalFriends", 1);
        var b = CreateUserForTest("InternalFriends", 2);
        await AcceptFriendshipAsync(a, b);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/friendships/validate-pair",
            new InternalSocialUserPairRequestDto(a, b),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task ValidateFriendshipPair_Returns400_WhenNotFriends()
    {
        var a = CreateUserForTest("InternalNotFriends", 1);
        var b = CreateUserForTest("InternalNotFriends", 2);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/friendships/validate-pair",
            new InternalSocialUserPairRequestDto(a, b),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.BadRequest,
            "You must be friends to open a direct chat.");
    }

    [Fact]
    public async Task ValidateFriendshipPair_Returns401_WhenInternalSecretMissing()
    {
        var a = CreateUserForTest("InternalNoSecret", 1);
        var b = CreateUserForTest("InternalNoSecret", 2);

        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/friendships/validate-pair",
            new InternalSocialUserPairRequestDto(a, b),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ValidateNoBlockBetweenUsers_Returns204_WhenNoBlock()
    {
        var a = CreateUserForTest("InternalNoBlock", 1);
        var b = CreateUserForTest("InternalNoBlock", 2);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/validate-between",
            new InternalSocialUserPairRequestDto(a, b),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task ValidateNoBlockBetweenUsers_Returns400_WhenBlockExists()
    {
        var a = CreateUserForTest("InternalBlockBetween", 1);
        var b = CreateUserForTest("InternalBlockBetween", 2);
        LoginAs(a);
        await BlockUserAsync(b);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/validate-between",
            new InternalSocialUserPairRequestDto(a, b),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.BadRequest,
            "Cannot chat while a block exists between you and this user.");
    }

    [Fact]
    public async Task ValidateNoBlockAgainstOthers_Returns204_WhenNoBlock()
    {
        var a = CreateUserForTest("InternalAgainstOthers", 1);
        var b = CreateUserForTest("InternalAgainstOthers", 2);
        var c = CreateUserForTest("InternalAgainstOthers", 3);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/validate-against-others",
            new InternalSocialMutualBlocksRequestDto(a, [b, c]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task ValidateNoBlockAgainstOthers_Returns400_WhenBlockExists()
    {
        var a = CreateUserForTest("InternalAgainstBlocked", 1);
        var b = CreateUserForTest("InternalAgainstBlocked", 2);
        var c = CreateUserForTest("InternalAgainstBlocked", 3);
        LoginAs(c);
        await BlockUserAsync(a);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/validate-against-others",
            new InternalSocialMutualBlocksRequestDto(a, [b, c]),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.BadRequest,
            "Cannot chat while a block exists between you and this user.");
    }

    [Fact]
    public async Task GetMutualBlockIds_ReturnsBlockedUserIds()
    {
        var a = CreateUserForTest("InternalMutual", 1);
        var b = CreateUserForTest("InternalMutual", 2);
        var c = CreateUserForTest("InternalMutual", 3);
        LoginAs(a);
        await BlockUserAsync(b);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/mutual-ids",
            new InternalSocialMutualBlocksRequestDto(a, [b, c]),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalSocialMutualBlocksResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal([b], body.BlockedUserIds);
    }

    [Fact]
    public async Task IsBlockedBy_ReturnsTrue_WhenBlockExists()
    {
        var blocker = CreateUserForTest("InternalIsBlocked", 1);
        var blocked = CreateUserForTest("InternalIsBlocked", 2);
        LoginAs(blocker);
        await BlockUserAsync(blocked);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/is-blocked-by",
            new InternalSocialIsBlockedRequestDto(blocker, blocked),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalSocialIsBlockedResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.True(body.IsBlocked);
    }

    [Fact]
    public async Task IsBlockedBy_ReturnsFalse_WhenNoBlock()
    {
        var blocker = CreateUserForTest("InternalNotBlocked", 1);
        var blocked = CreateUserForTest("InternalNotBlocked", 2);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"{InternalBase}/blocks/is-blocked-by",
            new InternalSocialIsBlockedRequestDto(blocker, blocked),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalSocialIsBlockedResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(body.IsBlocked);
    }

    [Fact]
    public async Task DetachOnDeletion_RemovesFriendshipsRequestsAndBlocks()
    {
        var a = CreateUserForTest("InternalDetach", 1);
        var b = CreateUserForTest("InternalDetach", 2);
        var c = CreateUserForTest("InternalDetach", 3);

        await AcceptFriendshipAsync(a, b);
        LoginAs(a);
        await SendFriendRequestAsync(c);
        await BlockUserAsync(c);

        SocialTestAuthHelpers.LoginAsInternal(Client);
        var detach = await Client.PostAsync(
            $"{InternalBase}/users/{a}/detach-on-deletion",
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);

        LoginAs(a);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken)).StatusCode);

        LoginAs(b);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);

        LoginAs(c);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }
}
