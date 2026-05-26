using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserBlockControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    private const string BlocksBase = "/api/users/blocks";

    private async Task BlockUserAsync(long blockedUserId)
    {
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private async Task<long> GetBlockIdAsync(long blockedUserId)
    {
        var res = await Client.GetAsync($"{BlocksBase}/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>();
        Assert.NotNull(list);
        return list.Single(b => b.BlockedUserId == blockedUserId).Id;
    }

    // --- CREATE ---

    [Fact]
    public async Task BlockUser_Returns200_WhenValid()
    {
        // Arrange
        const string testMethodName = "BlockCreate";
        var blocker = await CreateUserForTest(testMethodName, 1);
        var blocked = await CreateUserForTest(testMethodName, 2);
        await LoginAs(blocker);

        // Act
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task BlockUser_Returns400_WhenBlockingSelf()
    {
        // Arrange
        const string testMethodName = "BlockCreateSelf";
        var user = await CreateUserForTest(testMethodName, 1);
        await LoginAs(user);

        // Act
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = user.Id });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Cannot block yourself.", problem.Detail);
    }

    [Fact]
    public async Task BlockUser_Returns409_WhenAlreadyBlocked()
    {
        // Arrange
        const string testMethodName = "BlockCreateDuplicate";
        var blocker = await CreateUserForTest(testMethodName, 1);
        var blocked = await CreateUserForTest(testMethodName, 2);
        await LoginAs(blocker);
        await BlockUserAsync(blocked.Id);

        // Act
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal($"User {blocked.Id} is already blocked.", problem.Detail);
    }

    [Fact]
    public async Task BlockUser_Returns400_WhenBlockedUserMissing()
    {
        // Arrange
        const string testMethodName = "BlockCreateMissing";
        var blocker = await CreateUserForTest(testMethodName, 1);
        await LoginAs(blocker);

        // Act
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = 999999 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("User not found", problem.Detail);
    }

    // --- GET ---

    [Fact]
    public async Task GetMyBlocks_ReturnsBlockedUsers()
    {
        // Arrange
        const string testMethodName = "BlockList";
        var blocker = await CreateUserForTest(testMethodName, 1);
        var blocked = await CreateUserForTest(testMethodName, 2);
        await LoginAs(blocker);
        await BlockUserAsync(blocked.Id);

        // Act
        var res = await Client.GetAsync($"{BlocksBase}/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>();
        Assert.NotNull(list);
        var only = Assert.Single(list);
        Assert.Equal(blocked.Id, only.BlockedUserId);
        Assert.Equal($"{testMethodName}User2", only.BlockedUserNickname);
    }

    [Fact]
    public async Task GetMyBlocks_Returns204_WhenEmpty()
    {
        // Arrange
        const string testMethodName = "BlockListEmpty";
        var user = await CreateUserForTest(testMethodName, 1);
        await LoginAs(user);

        // Act
        var res = await Client.GetAsync($"{BlocksBase}/me");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteBlock_Returns204_AndRemovesBlock()
    {
        // Arrange
        const string testMethodName = "BlockRemove";
        var blocker = await CreateUserForTest(testMethodName, 1);
        var blocked = await CreateUserForTest(testMethodName, 2);
        await LoginAs(blocker);
        await BlockUserAsync(blocked.Id);
        var blockId = await GetBlockIdAsync(blocked.Id);

        // Act
        var res = await Client.DeleteAsync($"{BlocksBase}/{blockId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{BlocksBase}/me")).StatusCode);
    }

    [Fact]
    public async Task DeleteBlock_Returns401_WhenStranger()
    {
        // Arrange
        const string testMethodName = "BlockRemoveStranger";
        var blocker = await CreateUserForTest(testMethodName, 1);
        var blocked = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await LoginAs(blocker);
        await BlockUserAsync(blocked.Id);
        var blockId = await GetBlockIdAsync(blocked.Id);

        await LoginAs(stranger);

        // Act
        var res = await Client.DeleteAsync($"{BlocksBase}/{blockId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized access", problem.Detail);
    }

    [Fact]
    public async Task DeleteBlock_Returns201_WhenFriendRequestSentAfterUnblock()
    {
        // Arrange
        const string testMethodName = "BlockUnblockSend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        await BlockUserAsync(addressee.Id);

        var blockedSend = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        Assert.Equal(HttpStatusCode.BadRequest, blockedSend.StatusCode);
        var blockedSendProblem = await blockedSend.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(blockedSendProblem);
        Assert.Equal("Cannot send a friend request to a user you have blocked.", blockedSendProblem.Detail);

        var blockId = await GetBlockIdAsync(addressee.Id);

        // Act
        var delete = await Client.DeleteAsync($"{BlocksBase}/{blockId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var send = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
    }
}
