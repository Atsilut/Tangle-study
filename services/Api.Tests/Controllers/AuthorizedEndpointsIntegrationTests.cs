using System.Net;
using System.Net.Http.Json;
using Api.Domain.Friendships.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Posts.Dto;
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
                GroupIntegrationTestHelpers.GroupsBase,
                new GroupCreateRequestDto
                {
                    Name = "x",
                    Description = "y",
                    Visibility = GroupVisibility.Private,
                }
            },
            { HttpMethod.Get, $"{GroupIntegrationTestHelpers.GroupsBase}/1/boards", null },
            {
                HttpMethod.Post,
                $"{GroupIntegrationTestHelpers.GroupsBase}/1/blacklist",
                new GroupBlacklistCreateRequestDto { UserId = 2 }
            },
            { HttpMethod.Get, $"{GroupIntegrationTestHelpers.GroupsBase}/1/boards/1/posts", null },
            {
                HttpMethod.Post,
                "/api/posts",
                new PostCreateRequestDto { Title = "t", Content = "c" }
            },
            {
                HttpMethod.Post,
                "/api/friendships/requests",
                new FriendRequestCreateRequestDto { AddresseeId = 1 }
            },
            { HttpMethod.Patch, "/api/users", new UserPatchRequestDto(1, "x") },
            {
                HttpMethod.Patch,
                "/api/users/privacy",
                new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Private }
            },
        };

    [Theory]
    [MemberData(nameof(UnauthorizedEndpointData))]
    public async Task AuthorizedEndpoint_Returns401_WhenUnauthenticated(
        HttpMethod method,
        string url,
        object? body)
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var res = await SendAsync(method, url, body);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body) =>
        method.Method switch
        {
            "POST" when body is not null => Client.PostAsJsonAsync(url, body),
            "POST" => Client.PostAsync(url, null),
            "GET" => Client.GetAsync(url),
            "PATCH" when body is not null => Client.PatchAsJsonAsync(url, body),
            "PATCH" => Client.PatchAsync(url, null),
            "DELETE" => Client.DeleteAsync(url),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method."),
        };
}
