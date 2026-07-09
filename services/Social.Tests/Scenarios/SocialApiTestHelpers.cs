using System.Net;
using System.Net.Http.Json;
using Social.Dto;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;

namespace Social.Tests.Scenarios;

public static class SocialApiTestHelpers
{
    private const string RequestsBase = "/api/friendships/requests";

    public static async Task SendFriendRequestAsync(
        HttpClient client,
        long requesterUserId,
        long addresseeId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, requesterUserId, TestContext.Current.CancellationToken);
        var res = await client.PostAsJsonAsync(
            RequestsBase,
            new FriendRequestCreateRequestDto { AddresseeId = addresseeId },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
    }

    public static async Task AcceptFriendshipAsync(
        HttpClient client,
        long requesterUserId,
        long addresseeUserId,
        ITestAuth auth)
    {
        await SendFriendRequestAsync(client, requesterUserId, addresseeUserId, auth);
        await auth.AuthenticateAsync(client, addresseeUserId, TestContext.Current.CancellationToken);
        var pending = await client.GetAsync($"{RequestsBase}/pending", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(pending, HttpStatusCode.OK);
        var requests = await pending.Content.ReadFromJsonAsync<List<FriendRequestGetResponseDto>>(TestContext.Current.CancellationToken);
        var request = requests!.Single(r => r.OtherUserId == requesterUserId && r.IsIncoming);
        var accept = await client.PostAsync(
            $"{RequestsBase}/{request.Id}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(accept, HttpStatusCode.OK);
    }
}
