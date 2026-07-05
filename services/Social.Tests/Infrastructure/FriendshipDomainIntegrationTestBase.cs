using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Social.Client;
using Social.Db;
using Social.Dto;

namespace Social.Tests.Infrastructure;

public abstract class FriendshipDomainIntegrationTestBase(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    protected const string RequestsBase = "/api/friendships/requests";
    protected const string FriendshipsBase = "/api/friendships";
    protected const string BlocksBase = "/api/users/blocks";

    protected long CreateUserForTest(
        string testMethodName,
        long index = 1,
        FriendsListVisibility visibility = FriendsListVisibility.Private) =>
        InMemoryUser.SeedUser($"{testMethodName}User{index}", visibility: visibility);

    protected void LoginAs(long userId) => GatewayTestAuthHelpers.LoginAs(Client, userId);

    protected void SetFriendsListVisibility(long userId, FriendsListVisibility visibility) =>
        InMemoryUser.FriendsListVisibilityByUserId[userId] = visibility;

    protected async Task SendFriendRequestAsync(long addresseeId)
    {
        var res = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addresseeId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    protected async Task<FriendRequestGetResponseDto> GetPendingRequestAsync(long otherUserId, bool? isIncoming = null)
    {
        var res = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        return list.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
    }

    protected async Task<long> SendFriendRequestAndGetOutgoingIdAsync(long requester, long addressee)
    {
        LoginAs(requester);
        await SendFriendRequestAsync(addressee);
        return (await GetPendingRequestAsync(addressee, isIncoming: false)).Id;
    }

    protected async Task AcceptFriendshipAsync(long requester, long addressee)
    {
        LoginAs(requester);
        await SendFriendRequestAsync(addressee);
        LoginAs(addressee);
        var id = (await GetPendingRequestAsync(requester, isIncoming: true)).Id;
        var accept = await Client.PostAsync(
            $"{RequestsBase}/{id}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    protected async Task<FriendshipGetResponseDto> GetAcceptedFriendAsync(long otherUserId)
    {
        var res = await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        return list.Single(f => f.OtherUserId == otherUserId);
    }

    protected async Task BlockUserAsync(long blockedUserId)
    {
        var res = await Client.PostAsJsonAsync(BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    protected async Task UnblockUserAsync(long blockedUserId)
    {
        var listRes = await Client.GetAsync($"{BlocksBase}/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var blocks = await listRes.Content.ReadFromJsonAsync<List<UserBlockGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(blocks);
        var block = Assert.Single(blocks, b => b.BlockedUserId == blockedUserId);
        var res = await Client.DeleteAsync($"{BlocksBase}/{block.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    protected async Task IgnoreIncomingRequestAsync(long addressee, long requestId)
    {
        LoginAs(addressee);
        var res = await Client.PostAsync(
            $"{RequestsBase}/{requestId}/ignore",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    protected async Task ApplyFriendshipSetupStepsAsync(
        long userA,
        long userB,
        IReadOnlyList<FriendshipSetupStep> steps)
    {
        foreach (var step in steps)
        {
            switch (step)
            {
                case FriendshipSetupStep.SendAtoB:
                    await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
                    break;
                case FriendshipSetupStep.IgnoreByB:
                    LoginAs(userB);
                    var requestId = (await GetPendingRequestAsync(userA, isIncoming: true)).Id;
                    await IgnoreIncomingRequestAsync(userB, requestId);
                    break;
                case FriendshipSetupStep.UserBBlocksA:
                    LoginAs(userB);
                    await BlockUserAsync(userA);
                    break;
                case FriendshipSetupStep.UserABlocksB:
                    LoginAs(userA);
                    await BlockUserAsync(userB);
                    break;
            }
        }
    }

    protected async Task<HttpStatusCode> SendFriendRequestStatusAsync(long requester, long addressee)
    {
        LoginAs(requester);
        var res = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee },
            TestContext.Current.CancellationToken);
        return res.StatusCode;
    }

    protected async Task AssertNoPendingBetweenAsync(long userA, long userB)
    {
        LoginAs(userA);
        var aPending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        if (aPending.StatusCode == HttpStatusCode.OK)
        {
            var list = await aPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(list ?? [], p => p.OtherUserId == userB);
        }

        LoginAs(userB);
        var bPending = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        if (bPending.StatusCode == HttpStatusCode.OK)
        {
            var list = await bPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(list ?? [], p => p.OtherUserId == userA);
        }
    }

    protected async Task AssertFriendshipExistsAsync(long userA, long userB, bool expected)
    {
        LoginAs(userA);
        var res = await Client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);
        if (!expected)
        {
            if (res.StatusCode == HttpStatusCode.NoContent) return;
            var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(list ?? [], f => f.OtherUserId == userB);
            return;
        }

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var friends = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(friends);
        Assert.Contains(friends, f => f.OtherUserId == userB);
    }

    protected async Task AssertPendingDtoAppearsAsync(
        long viewer,
        long otherUserId,
        bool appearsPending,
        bool? isIncoming = null)
    {
        LoginAs(viewer);
        var res = await Client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        var dto = list.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
        Assert.Equal(appearsPending, dto.IsPending);
    }

    protected async Task AssertStoredFriendRequestIsPendingAsync(
        long requesterId,
        long addresseeId,
        bool isPending)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
        var request = await db.FriendRequests.SingleOrDefaultAsync(r =>
            r.RequesterId == requesterId && r.AddresseeId == addresseeId,
            TestContext.Current.CancellationToken);
        Assert.NotNull(request);
        Assert.Equal(isPending, request.IsPending);
    }
}
