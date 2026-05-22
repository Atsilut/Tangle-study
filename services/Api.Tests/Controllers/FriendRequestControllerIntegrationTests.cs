using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FriendRequestControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    // --- CREATE ---

    [Fact]
    public async Task SendRequest_Returns201_AndPendingFriendRequest()
    {
        // Arrange
        const string testMethodName = "FriendSend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);

        // Act
        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var pending = await GetPendingRequestAsync(addressee.Id, isIncoming: false);
        Assert.True(pending.IsPending);
        Assert.Equal(addressee.Id, pending.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        const string testMethodName = "FriendSendUnauth";
        var addressee = await CreateUserForTest(testMethodName, 1);
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeMissing()
    {
        // Arrange
        const string testMethodName = "FriendSendMissing";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        // Act
        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = 999999 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeIsSelf()
    {
        // Arrange
        const string testMethodName = "FriendSendSelf";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        // Act
        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns201_WhenDuplicateOutgoingRequest()
    {
        // Arrange
        const string testMethodName = "FriendSendDup";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await LoginAs(a);
        var first = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = b.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act
        var dup = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = b.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, dup.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns200_AndCreatesFriendship_WhenAddresseeSendsBackToPendingRequester()
    {
        // Arrange
        const string testMethodName = "FriendSendReciprocal";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        var first = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await LoginAs(addressee);

        // Act
        var reciprocal = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        // Assert
        Assert.Equal(HttpStatusCode.OK, reciprocal.StatusCode);
        await LoginAs(addressee);
        Assert.Equal(requester.Id, (await GetAcceptedFriendAsync(requester.Id)).OtherUserId);

        var pending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns200_WhenAddresseeSendsBackAfterIgnoring()
    {
        // Arrange
        const string testMethodName = "FriendIgnoreReciprocal";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        await LoginAs(addressee);
        var ignore = await Client.PostAsync($"{RequestsBase}/{requestId}/ignore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);
        await LoginAs(addressee);

        // Act
        var reciprocal = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        // Assert
        Assert.Equal(HttpStatusCode.OK, reciprocal.StatusCode);
        Assert.Equal(requester.Id, (await GetAcceptedFriendAsync(requester.Id)).OtherUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{RequestsBase}/pending")).StatusCode);
    }

    // --- GET ---

    [Fact]
    public async Task GetPending_ListsIncomingAndOutgoing()
    {
        // Arrange
        const string testMethodName = "FriendPending";
        var me = await CreateUserForTest(testMethodName, 1);
        var outgoing = await CreateUserForTest(testMethodName, 2);
        var incoming = await CreateUserForTest(testMethodName, 3);

        await LoginAs(me);
        await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = outgoing.Id });
        await LoginAs(incoming);
        await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = me.Id });

        await LoginAs(me);

        // Act
        var res = await Client.GetAsync($"{RequestsBase}/pending");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(list, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }

    // --- UPDATE ---

    [Fact]
    public async Task Accept_Returns200_AndCreatesFriendship()
    {
        // Arrange
        const string testMethodName = "FriendAccept";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);

        // Act
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await LoginAs(addressee);
        Assert.Equal(requester.Id, (await GetAcceptedFriendAsync(requester.Id)).OtherUserId);

        var pending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns400_WhenAddresseeBlockedRequester()
    {
        // Arrange
        const string testMethodName = "FriendAcceptBlocked";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var block = await Client.PostAsJsonAsync("/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        // Act
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await LoginAs(addressee);
        var friends = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.NoContent, friends.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns401_WhenCalledByRequester()
    {
        // Arrange
        const string testMethodName = "FriendAcceptUnauth";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(requester);

        // Act
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- DELETE ---

    [Fact]
    public async Task IgnoreRequest_Returns204_IgnoresForAddressee_RequesterStillSeesPending()
    {
        // Arrange
        const string testMethodName = "FriendIgnore";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);

        // Act
        var ignore = await Client.PostAsync($"{RequestsBase}/{requestId}/ignore", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);
        var addresseePending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, addresseePending.StatusCode);

        var ignored = await Client.GetAsync($"{RequestsBase}/ignored");
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
        var ignoredList = await ignored.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.NotNull(ignoredList);
        Assert.Contains(ignoredList, d => d.Id == requestId && !d.IsPending);

        await LoginAs(requester);
        var requesterPending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, requesterPending.StatusCode);
        var requesterList = await requesterPending.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.NotNull(requesterList);
        var outgoing = Assert.Single(requesterList);
        Assert.True(outgoing.IsPending);
        Assert.Equal(addressee.Id, outgoing.OtherUserId);

        var resend = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
    }

    [Fact]
    public async Task BlockUser_Returns200_AndBlocksOutgoingFriendRequest()
    {
        // Arrange
        const string testMethodName = "UserBlockOutgoing";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);
        var block = await Client.PostAsJsonAsync("/api/users/blocks", new UserBlockCreateRequestDto { BlockedUserId = addressee.Id });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        // Act
        var send = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
    }

    [Fact]
    public async Task BlockUser_IgnoresIncomingRequest_AndBlocksResendReactivation()
    {
        // Arrange
        const string testMethodName = "UserBlockResend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var block = await Client.PostAsJsonAsync("/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var addresseePending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, addresseePending.StatusCode);

        await LoginAs(requester);

        // Act
        var resend = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        var requesterPending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, requesterPending.StatusCode);
        var requesterList = await requesterPending.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.NotNull(requesterList);
        var outgoing = Assert.Single(requesterList);
        Assert.True(outgoing.IsPending);
        Assert.Equal(addressee.Id, outgoing.OtherUserId);

        await LoginAs(addressee);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{RequestsBase}/pending")).StatusCode);
    }

    [Fact]
    public async Task BlockUser_BlocksAddresseeFromSendingRequest()
    {
        // Arrange
        const string testMethodName = "UserBlockReciprocal";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(addressee);
        var block = await Client.PostAsJsonAsync("/api/users/blocks", new UserBlockCreateRequestDto { BlockedUserId = requester.Id });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        // Act
        var send = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
    }

    [Fact]
    public async Task CancelRequest_Returns204_AndRemovesPendingOutgoing()
    {
        // Arrange
        const string testMethodName = "FriendCancel";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        await LoginAs(requester);

        // Act
        var cancel = await Client.DeleteAsync($"{RequestsBase}/{requestId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{RequestsBase}/pending")).StatusCode);
        await LoginAs(addressee);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{RequestsBase}/pending")).StatusCode);
    }

    [Fact]
    public async Task Reject_Returns204_AndRemovesRequest()
    {
        // Arrange
        const string testMethodName = "FriendReject";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);

        // Act
        var res = await Client.DeleteAsync($"{RequestsBase}/{requestId}/reject");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var pending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_CascadesFriendRequestsAndFriendships()
    {
        // Arrange
        const string testMethodName = "UserDeleteCascade";
        var deleter = await CreateUserForTest(testMethodName, 1);
        var other = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(deleter, other);
        await LoginAs(deleter);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{deleter.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        await LoginAs(other);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{RequestsBase}/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync($"{FriendshipsBase}/me")).StatusCode);
    }
}
