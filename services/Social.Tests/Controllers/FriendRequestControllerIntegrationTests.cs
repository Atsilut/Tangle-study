using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Social.Friendships.Dto;
using Social.Tests.Infrastructure;
using Social.UserBlocks.Dto;

namespace Social.Tests.Controllers;

[Collection(SocialIntegrationTestCollection.Name)]
public sealed class FriendRequestControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : FriendshipDomainIntegrationTestBase(postgres)
{
    [Fact]
    public async Task SendRequest_Returns201_AndPendingFriendRequest()
    {
        const string testMethodName = "FriendSend";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(requester);

        var res = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var pending = await GetPendingRequestAsync(addressee, isIncoming: false);
        Assert.True(pending.IsPending);
        Assert.Equal(addressee, pending.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns401_WhenNotAuthenticated()
    {
        const string testMethodName = "FriendSendUnauth";
        var addressee = CreateUserForTest(testMethodName, 1);
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeMissing()
    {
        const string testMethodName = "FriendSendMissing";
        var requester = CreateUserForTest(testMethodName, 1);
        LoginAs(requester);

        var res = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = 999999 },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "Addressee not found");
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeIsSelf()
    {
        const string testMethodName = "FriendSendSelf";
        var requester = CreateUserForTest(testMethodName, 1);
        LoginAs(requester);

        var res = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot send a friend request to yourself.", problem.Detail);
    }

    [Fact]
    public async Task SendRequest_Returns201_WhenDuplicateOutgoingRequest()
    {
        const string testMethodName = "FriendSendDup";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(requester);
        var first = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var dup = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, dup.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns200_AndCreatesFriendship_WhenAddresseeSendsBackToPendingRequester()
    {
        const string testMethodName = "FriendSendReciprocal";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(requester);
        var first = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        LoginAs(addressee);

        var reciprocal = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, reciprocal.StatusCode);
        LoginAs(addressee);
        Assert.Equal(requester, (await GetAcceptedFriendAsync(requester)).OtherUserId);

        var pending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns200_WhenAddresseeSendsBackAfterIgnoring()
    {
        const string testMethodName = "FriendIgnoreReciprocal";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        LoginAs(addressee);
        var ignore = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/ignore",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);

        var reciprocal = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, reciprocal.StatusCode);
        Assert.Equal(requester, (await GetAcceptedFriendAsync(requester)).OtherUserId);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task GetPending_ListsIncomingAndOutgoing()
    {
        const string testMethodName = "FriendPending";
        var me = CreateUserForTest(testMethodName, 1);
        var outgoing = CreateUserForTest(testMethodName, 2);
        var incoming = CreateUserForTest(testMethodName, 3);

        LoginAs(me);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = outgoing },
            TestContext.Current.CancellationToken);
        LoginAs(incoming);
        await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = me },
            TestContext.Current.CancellationToken);

        LoginAs(me);

        var res = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.OtherUserId == outgoing && !p.IsIncoming);
        Assert.Contains(list, p => p.OtherUserId == incoming && p.IsIncoming);
    }

    [Fact]
    public async Task Accept_Returns200_AndCreatesFriendship()
    {
        const string testMethodName = "FriendAccept";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        LoginAs(addressee);
        Assert.Equal(requester, (await GetAcceptedFriendAsync(requester)).OtherUserId);

        var pending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns400_WhenAddresseeBlockedRequester()
    {
        const string testMethodName = "FriendAcceptBlocked";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);
        await BlockUserAsync(requester);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot form a friendship while a block exists between you and this user.", problem.Detail);
        LoginAs(addressee);
        var friends = await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, friends.StatusCode);
    }

    [Fact]
    public async Task IgnoreRequest_Returns204_WhenAlreadyIgnoredByBlock()
    {
        const string testMethodName = "FriendIgnoreAfterBlock";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);
        await BlockUserAsync(requester);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/ignore",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        LoginAs(addressee);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Accept_Returns404_WhenRequesterBlockedAddresseeThenUnblocked()
    {
        const string testMethodName = "FriendAcceptAfterUnblock";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(requester);
        await BlockUserAsync(addressee);
        var blocks = await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken);
        var blockList = await blocks.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(blockList);
        var block = Assert.Single(blockList);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"{BlocksBase}/{block.Id}", TestContext.Current.CancellationToken)).StatusCode);

        LoginAs(addressee);
        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Friend request not found", problem.Detail);
        LoginAs(addressee);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Accept_Returns401_WhenCalledByRequester()
    {
        const string testMethodName = "FriendAcceptUnauth";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(requester);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Only the addressee can act on this friend request.", problem.Detail);
    }

    [Fact]
    public async Task IgnoreRequest_Returns204_IgnoresForAddressee_RequesterStillSeesPending()
    {
        const string testMethodName = "FriendIgnore";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);

        var ignore = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/ignore",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, ignore.StatusCode);
        var addresseePending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, addresseePending.StatusCode);

        var ignored = await Client.GetAsync($"{RequestsBase}/ignored", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
        var ignoredList = await ignored.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(ignoredList);
        Assert.Contains(ignoredList, d => d.Id == requestId && !d.IsPending);

        LoginAs(requester);
        var requesterPending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, requesterPending.StatusCode);
        var requesterList = await requesterPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(requesterList);
        var outgoing = Assert.Single(requesterList);
        Assert.True(outgoing.IsPending);
        Assert.Equal(addressee, outgoing.OtherUserId);

        var resend = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);

        LoginAs(addressee);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
        var ignoredAfterResend = await Client.GetAsync($"{RequestsBase}/ignored", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ignoredAfterResend.StatusCode);
        var ignoredAfterResendList = await ignoredAfterResend.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(ignoredAfterResendList);
        Assert.Contains(ignoredAfterResendList, d => d.Id == requestId && !d.IsPending);

        LoginAs(requester);
        var requesterPendingAfterResend = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, requesterPendingAfterResend.StatusCode);
        var requesterListAfterResend = await requesterPendingAfterResend.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(requesterListAfterResend);
        var outgoingAfterResend = Assert.Single(requesterListAfterResend);
        Assert.True(outgoingAfterResend.IsPending);
        Assert.Equal(addressee, outgoingAfterResend.OtherUserId);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenRequesterBlockedAddressee()
    {
        const string testMethodName = "FriendSendBlocked";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(requester);
        await BlockUserAsync(addressee);

        var send = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        var problem = await send.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot send a friend request to a user you have blocked.", problem.Detail);
    }

    [Fact]
    public async Task SendRequest_Returns201_WhenAddresseeBlockedRequester()
    {
        const string testMethodName = "FriendSendBlockedByAddressee";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);
        await BlockUserAsync(requester);

        var addresseePending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, addresseePending.StatusCode);

        LoginAs(requester);

        var resend = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        var requesterPending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, requesterPending.StatusCode);
        var requesterList = await requesterPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(requesterList);
        var outgoing = Assert.Single(requesterList);
        Assert.True(outgoing.IsPending);
        Assert.Equal(addressee, outgoing.OtherUserId);

        LoginAs(addressee);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenAddresseeBlockedRequester()
    {
        const string testMethodName = "FriendSendBlockedIncoming";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        LoginAs(addressee);
        await BlockUserAsync(requester);

        var send = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        var problem = await send.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot send a friend request to a user you have blocked.", problem.Detail);
    }

    [Fact]
    public async Task CancelRequest_Returns204_AndRemovesPendingOutgoing()
    {
        const string testMethodName = "FriendCancel";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        LoginAs(requester);

        var cancel = await Client.DeleteAsync($"{RequestsBase}/{requestId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
        LoginAs(addressee);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Reject_Returns204_AndRemovesRequest()
    {
        const string testMethodName = "FriendReject";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);

        LoginAs(addressee);

        var res = await Client.DeleteAsync($"{RequestsBase}/{requestId}/reject", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var pending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    [Fact]
    public async Task SendRequest_Returns400_WhenIgnoredBlockedThenAddresseeSendsBack()
    {
        const string testMethodName = "FriendIgnoreBlockRecip";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        LoginAs(addressee);
        await IgnoreIncomingRequestAsync(addressee, requestId);
        await BlockUserAsync(requester);

        var send = await Client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = requester },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        var problem = await send.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot send a friend request to a user you have blocked.", problem.Detail);
        LoginAs(requester);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Accept_Returns200_WhenIgnoredIncomingWithoutBlock()
    {
        const string testMethodName = "FriendAcceptIgnored";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        await IgnoreIncomingRequestAsync(addressee, requestId);
        LoginAs(addressee);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(requester, (await GetAcceptedFriendAsync(requester)).OtherUserId);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Reject_Returns204_WhenIgnoredIncoming()
    {
        const string testMethodName = "FriendRejectIgnored";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        await IgnoreIncomingRequestAsync(addressee, requestId);
        LoginAs(addressee);

        var res = await Client.DeleteAsync($"{RequestsBase}/{requestId}/reject", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
        LoginAs(requester);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Accept_Returns400_WhenIgnoredIncomingAndBlocked()
    {
        const string testMethodName = "FriendAcceptIgnBlk";
        var requester = CreateUserForTest(testMethodName, 1);
        var addressee = CreateUserForTest(testMethodName, 2);
        var requestId = await SendFriendRequestAndGetOutgoingIdAsync(requester, addressee);
        await IgnoreIncomingRequestAsync(addressee, requestId);
        await BlockUserAsync(requester);
        LoginAs(addressee);

        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal("Cannot form a friendship while a block exists between you and this user.", problem.Detail);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken)).StatusCode);
    }
}
