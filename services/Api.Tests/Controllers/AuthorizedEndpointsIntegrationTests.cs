using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthorizedEndpointsIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    public static TheoryData<HttpMethod, string, object?> UnauthorizedEndpointData =>
        new()
        {
            {
                HttpMethod.Post,
                "/api/friendships/requests",
                new FriendRequestCreateRequestDto { AddresseeId = 1 }
            },
            { HttpMethod.Patch, "/api/users", new UserPatchRequestDto { Id = 1, Nickname = "x" } },
            {
                HttpMethod.Patch,
                "/api/users/privacy",
                new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Private }
            },
            {
                HttpMethod.Post,
                "/api/users/blocks",
                new UserBlockCreateRequestDto { BlockedUserId = 1 }
            },
            { HttpMethod.Get, "/api/users/blocks/me", null },
            { HttpMethod.Delete, "/api/users/blocks/1", null },
        };

    [Theory]
    [MemberData(nameof(UnauthorizedEndpointData))]
    public async Task AuthorizedEndpoint_Returns401_WhenUnauthenticated(
        HttpMethod method,
        string url,
        object? body)
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await SendAsync(method, url, body);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body) =>
        method.Method switch
        {
            "POST" when body is not null => Client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken),
            "POST" => Client.PostAsync(url, null, TestContext.Current.CancellationToken),
            "GET" => Client.GetAsync(url, TestContext.Current.CancellationToken),
            "PATCH" when body is not null => Client.PatchAsJsonAsync(url, body, TestContext.Current.CancellationToken),
            "PATCH" => Client.PatchAsync(url, null),
            "DELETE" => Client.DeleteAsync(url, TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
        };
}
