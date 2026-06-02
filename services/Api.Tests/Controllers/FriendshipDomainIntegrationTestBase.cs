using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Controllers;

public abstract class FriendshipDomainIntegrationTestBase(
    PostgresTestcontainerFixture postgres,
    bool redisEnabled = false,
    string? redisConnectionString = null)
    : IntegrationTestBase(postgres, redisEnabled, redisConnectionString)
{
    protected const string RequestsBase = "/api/friendships/requests";
    protected const string FriendshipsBase = "/api/friendships";

    protected Task<UserGetResponseDto> CreateUserForTest(string testMethodName, long index = 1, string? password = null) =>
        IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index, password);

    protected Task LoginAs(UserGetResponseDto user, string? password = null) =>
        IntegrationTestAuthHelpers.LoginAsAsync(Client, user, password);

    protected async Task SetFriendsListVisibilityAsync(UserGetResponseDto user, FriendsListVisibility visibility)
    {
        await LoginAs(user);
        var res = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = visibility });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    protected async Task SendFriendRequestAsync(long addresseeId)
    {
        var res = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addresseeId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    protected async Task<FriendRequestGetResponseDto> GetPendingRequestAsync(long otherUserId, bool? isIncoming = null)
    {
        var res = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>();
        Assert.NotNull(list);
        return list.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
    }

    protected async Task<long> SendFriendRequestAndGetOutgoingIdAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester);
        await SendFriendRequestAsync(addressee.Id);
        return (await GetPendingRequestAsync(addressee.Id, isIncoming: false)).Id;
    }

    protected async Task AcceptFriendshipAsync(UserGetResponseDto requester, UserGetResponseDto addressee)
    {
        await LoginAs(requester);
        await SendFriendRequestAsync(addressee.Id);
        await LoginAs(addressee);
        var id = (await GetPendingRequestAsync(requester.Id, isIncoming: true)).Id;
        var accept = await Client.PostAsync($"{RequestsBase}/{id}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
    }

    protected async Task<FriendshipGetResponseDto> GetAcceptedFriendAsync(long otherUserId)
    {
        var res = await Client.GetAsync($"{FriendshipsBase}/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>();
        Assert.NotNull(list);
        return list.Single(f => f.OtherUserId == otherUserId);
    }

    protected async Task BlockUserAsync(long blockedUserId)
    {
        var res = await Client.PostAsJsonAsync("/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    protected async Task IgnoreIncomingRequestAsync(UserGetResponseDto addressee, long requestId)
    {
        await LoginAs(addressee);
        var res = await Client.PostAsync($"{RequestsBase}/{requestId}/ignore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    protected async Task ApplyFriendshipSetupStepsAsync(
        UserGetResponseDto userA,
        UserGetResponseDto userB,
        IReadOnlyList<FriendshipSetupStep> steps)
    {
        long? requestId = null;
        foreach (var step in steps)
        {
            switch (step)
            {
                case FriendshipSetupStep.SendAtoB:
                    requestId = await SendFriendRequestAndGetOutgoingIdAsync(userA, userB);
                    break;
                case FriendshipSetupStep.IgnoreByB:
                    await LoginAs(userB);
                    requestId = (await GetPendingRequestAsync(userA.Id, isIncoming: true)).Id;
                    await IgnoreIncomingRequestAsync(userB, requestId.Value);
                    break;
                case FriendshipSetupStep.UserBBlocksA:
                    await LoginAs(userB);
                    await BlockUserAsync(userA.Id);
                    break;
                case FriendshipSetupStep.UserABlocksB:
                    await LoginAs(userA);
                    await BlockUserAsync(userB.Id);
                    break;
            }
        }
    }

    protected async Task<HttpStatusCode> SendFriendRequestStatusAsync(
        UserGetResponseDto requester,
        UserGetResponseDto addressee)
    {
        await LoginAs(requester);
        var res = await Client.PostAsJsonAsync(RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addressee.Id });
        return res.StatusCode;
    }

    protected async Task AssertNoPendingBetweenAsync(UserGetResponseDto userA, UserGetResponseDto userB)
    {
        await LoginAs(userA);
        var aPending = await Client.GetAsync($"{RequestsBase}/pending");
        if (aPending.StatusCode == HttpStatusCode.OK)
        {
            var list = await aPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>();
            Assert.DoesNotContain(list ?? [], p => p.OtherUserId == userB.Id);
        }

        await LoginAs(userB);
        var bPending = await Client.GetAsync($"{RequestsBase}/pending");
        if (bPending.StatusCode == HttpStatusCode.OK)
        {
            var list = await bPending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>();
            Assert.DoesNotContain(list ?? [], p => p.OtherUserId == userA.Id);
        }
    }

    protected async Task AssertFriendshipExistsAsync(
        UserGetResponseDto userA,
        UserGetResponseDto userB,
        bool expected)
    {
        await LoginAs(userA);
        var res = await Client.GetAsync($"{FriendshipsBase}/me");
        if (!expected)
        {
            if (res.StatusCode == HttpStatusCode.NoContent)
                return;
            var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>();
            Assert.DoesNotContain(list ?? [], f => f.OtherUserId == userB.Id);
            return;
        }

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var friends = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>();
        Assert.Contains(friends!, f => f.OtherUserId == userB.Id);
    }

    protected async Task AssertPendingDtoAppearsAsync(
        UserGetResponseDto viewer,
        long otherUserId,
        bool appearsPending,
        bool? isIncoming = null)
    {
        await LoginAs(viewer);
        var res = await Client.GetAsync($"{RequestsBase}/pending");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>();
        var dto = list!.Single(p => p.OtherUserId == otherUserId && (isIncoming == null || p.IsIncoming == isIncoming));
        Assert.Equal(appearsPending, dto.IsPending);
    }

    protected async Task AssertStoredFriendRequestIsPendingAsync(
        long requesterId,
        long addresseeId,
        bool isPending)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.FriendRequests.SingleOrDefaultAsync(r =>
            r.RequesterId == requesterId && r.AddresseeId == addresseeId);
        Assert.NotNull(request);
        Assert.Equal(isPending, request!.IsPending);
    }
}