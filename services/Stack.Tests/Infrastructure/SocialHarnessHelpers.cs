using System.Net;
using System.Net.Http.Json;
using Social.Dto;
using Stack.Tests.Scenarios;
using Tangle.TestSupport.Auth;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

internal static class SocialHarnessHelpers
{
    private const string FriendshipsBase = "/api/friendships";
    private const string BlocksBase = "/api/users/blocks";

    private static ITestAuth Auth => HarnessJwtAuth.Instance;

    public static async Task BlockUserAsync(HttpClient client, UserGetResponseDto blocker, long blockedUserId)
    {
        await HarnessAuthHelpers.LoginAsAsync(client, blocker);
        var res = await client.PostAsJsonAsync(
            BlocksBase,
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
    }

    public static Task AcceptFriendshipAsync(
        HttpClient client,
        UserGetResponseDto requester,
        UserGetResponseDto addressee) =>
        SocialApiTestHelpers.AcceptFriendshipAsync(client, requester.Id, addressee.Id, Auth);

    public static async Task<FriendshipGetResponseDto> GetAcceptedFriendAsync(HttpClient client, long otherUserId)
    {
        var res = await client.GetAsync($"{FriendshipsBase}/me", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<FriendshipGetResponseDto>>(TestContext.Current.CancellationToken);
        return list!.Single(f => f.OtherUserId == otherUserId);
    }
}
