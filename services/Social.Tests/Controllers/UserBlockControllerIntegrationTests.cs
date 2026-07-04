using System.Net;
using System.Net.Http.Json;
using Social.Friendships.Dto;
using Social.Tests.Infrastructure;
using Social.UserBlocks.Dto;

namespace Social.Tests.Controllers;

[Collection(SocialIntegrationTestCollection.Name)]
public sealed class UserBlockControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    private async Task<long> GetBlockIdAsync(long blockedUserId)
    {
        var res = await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        return list.Single(b => b.BlockedUserId == blockedUserId).Id;
    }

    [Fact]
    public async Task BlockUser_Returns200_WhenValid()
    {
        const string testMethodName = "BlockCreate";
        var blocker = CreateUserForTest(testMethodName, 1);
        var blocked = CreateUserForTest(testMethodName, 2);
        LoginAs(blocker);

        var res = await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blocked },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task BlockUser_Returns400_WhenBlockingSelf()
    {
        const string testMethodName = "BlockCreateSelf";
        var user = CreateUserForTest(testMethodName, 1);
        LoginAs(user);

        var res = await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = user },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "Cannot block yourself.");
    }

    [Fact]
    public async Task BlockUser_Returns409_WhenAlreadyBlocked()
    {
        const string testMethodName = "BlockCreateDuplicate";
        var blocker = CreateUserForTest(testMethodName, 1);
        var blocked = CreateUserForTest(testMethodName, 2);
        LoginAs(blocker);
        await BlockUserAsync(blocked);

        var res = await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blocked },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(
            res, HttpStatusCode.Conflict, $"User {blocked} is already blocked.");
    }

    [Fact]
    public async Task BlockUser_Returns400_WhenBlockedUserMissing()
    {
        const string testMethodName = "BlockCreateMissing";
        var blocker = CreateUserForTest(testMethodName, 1);
        LoginAs(blocker);

        var res = await Client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = 999999 },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "User not found");
    }

    [Fact]
    public async Task GetMyBlocks_ReturnsBlockedUsers()
    {
        const string testMethodName = "BlockList";
        var blocker = CreateUserForTest(testMethodName, 1);
        var blocked = CreateUserForTest(testMethodName, 2);
        LoginAs(blocker);
        await BlockUserAsync(blocked);

        var res = await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        var only = Assert.Single(list);
        Assert.Equal(blocked, only.BlockedUserId);
        Assert.Equal($"{testMethodName}User2", only.BlockedUserNickname);
    }

    [Fact]
    public async Task GetMyBlocks_Returns204_WhenEmpty()
    {
        const string testMethodName = "BlockListEmpty";
        var user = CreateUserForTest(testMethodName, 1);
        LoginAs(user);

        var res = await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task DeleteBlock_Returns204_AndRemovesBlock()
    {
        const string testMethodName = "BlockRemove";
        var blocker = CreateUserForTest(testMethodName, 1);
        var blocked = CreateUserForTest(testMethodName, 2);
        LoginAs(blocker);
        await BlockUserAsync(blocked);
        var blockId = await GetBlockIdAsync(blocked);

        var res = await Client.DeleteAsync($"{BlocksBase}/{blockId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task DeleteBlock_Returns401_WhenStranger()
    {
        const string testMethodName = "BlockRemoveStranger";
        var blocker = CreateUserForTest(testMethodName, 1);
        var blocked = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        LoginAs(blocker);
        await BlockUserAsync(blocked);
        var blockId = await GetBlockIdAsync(blocked);

        LoginAs(stranger);

        var res = await Client.DeleteAsync($"{BlocksBase}/{blockId}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.Unauthorized, "Unauthorized access");
    }

    [Fact]
    public async Task DeleteBlock_Returns404_WhenNotFound()
    {
        const string testMethodName = "BlockRemoveMissing";
        var blocker = CreateUserForTest(testMethodName, 1);
        LoginAs(blocker);

        var res = await Client.DeleteAsync($"{BlocksBase}/999999", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Block not found");
    }

    [Fact]
    public async Task DeleteBlock_Returns201_WhenFriendRequestSentAfterUnblock()
    {
        const string testMethodName = "BlockUnblockSend";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(requester);
        await BlockUserAsync(addressee);

        var blockedSend = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertProblemDetailAsync(
            blockedSend,
            HttpStatusCode.BadRequest,
            "Cannot send a friend request to a user you have blocked.");

        var blockId = await GetBlockIdAsync(addressee);

        var delete = await Client.DeleteAsync($"{BlocksBase}/{blockId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var send = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
    }
}
