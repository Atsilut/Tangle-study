using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
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
    public async Task SendRequest_Returns409_WhenAlreadyExists()
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
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
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
}
