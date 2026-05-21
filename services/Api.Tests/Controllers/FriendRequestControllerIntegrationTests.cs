using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FriendRequestControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    [Fact]
    public async Task SendRequest_Returns201_AndPendingFriendRequest()
    {
        const string testMethodName = "FriendSend";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var pending = await GetPendingRequestAsync(addressee.Id, isIncoming: false);
        Assert.True(pending.IsPending);
        Assert.Equal(addressee.Id, pending.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns401_WhenNotAuthenticated()
    {
        const string testMethodName = "FriendSendUnauth";
        var addressee = await CreateUserForTest(testMethodName, 1);
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeMissing()
    {
        const string testMethodName = "FriendSendMissing";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = 999999 });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeIsSelf()
    {
        const string testMethodName = "FriendSendSelf";
        var requester = await CreateUserForTest(testMethodName, 1);
        await LoginAs(requester);

        var res = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = requester.Id });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns409_WhenAlreadyExists()
    {
        const string testMethodName = "FriendSendDup";
        var a = await CreateUserForTest(testMethodName, 1);
        var b = await CreateUserForTest(testMethodName, 2);
        await LoginAs(a);
        var first = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = b.Id });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var dup = await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = b.Id });

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task GetPending_ListsIncomingAndOutgoing()
    {
        const string testMethodName = "FriendPending";
        var me = await CreateUserForTest(testMethodName, 1);
        var outgoing = await CreateUserForTest(testMethodName, 2);
        var incoming = await CreateUserForTest(testMethodName, 3);

        await LoginAs(me);
        await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = outgoing.Id });
        await LoginAs(incoming);
        await Client.PostAsJsonAsync(RequestsBase, new FriendRequestCreateRequestDto { AddresseeId = me.Id });

        await LoginAs(me);
        var res = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.OtherUserId == outgoing.Id && !p.IsIncoming);
        Assert.Contains(list, p => p.OtherUserId == incoming.Id && p.IsIncoming);
    }

    [Fact]
    public async Task Accept_Returns200_AndCreatesFriendship()
    {
        const string testMethodName = "FriendAccept";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await LoginAs(addressee);
        Assert.Equal(requester.Id, (await GetAcceptedFriendAsync(requester.Id)).OtherUserId);

        var pending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns401_WhenCalledByRequester()
    {
        const string testMethodName = "FriendAcceptUnauth";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(requester);
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Reject_Returns204_AndRemovesRequest()
    {
        const string testMethodName = "FriendReject";
        var requester = await CreateUserForTest(testMethodName, 1);
        var addressee = await CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        await LoginAs(addressee);
        var res = await Client.DeleteAsync($"{RequestsBase}/{requestId}/reject");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var pending = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }
}
