using System.Net;
using System.Net.Http.Json;
using Users.Domain;
using Users.Dto;
using Users.Tests.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class AuthorizedEndpointsIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    public static TheoryData<HttpMethod, string, object?> UnauthorizedEndpointData =>
        new()
        {
            { HttpMethod.Patch, "/api/users", new UserPatchRequestDto { Id = 1, Nickname = "x" } },
            {
                HttpMethod.Patch,
                "/api/users/privacy",
                new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Private }
            },
            { HttpMethod.Delete, "/api/users/1", null },
        };

    [Theory]
    [MemberData(nameof(UnauthorizedEndpointData))]
    public async Task AuthorizedEndpoint_Returns401_WhenUnauthenticated(
        HttpMethod method,
        string url,
        object? body)
    {
        GatewayTestAuthHelpers.ClearAuth(Client);

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
