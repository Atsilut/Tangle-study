using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

public static class HarnessAuthHelpers
{
    public const string DefaultPassword = "testtest123!";

    private static readonly ConcurrentDictionary<long, string> TestEmailsByUserId = new();

    public static async Task<UserGetResponseDto> CreateUserForTestAsync(
        HttpClient client,
        string testMethodName,
        long index = 1,
        string? password = null)
    {
        password ??= DefaultPassword;
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = TestUserIdentity.BuildNickname(testMethodName, index);
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await client.PostAsJsonAsync("/api/join", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(create, HttpStatusCode.Created);

        var getAll = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>(TestContext.Current.CancellationToken);
        var profile = all!.Single(u => u.Nickname == req.Nickname);
        TestEmailsByUserId[profile.Id] = email;
        return profile;
    }

    public static Task LoginAsAsync(HttpClient client, UserGetResponseDto user, string? password = null) =>
        LoginAsAsync(client, user.Id, password);

    public static async Task LoginAsAsync(HttpClient client, long userId, string? password = null)
    {
        password ??= DefaultPassword;
        var email = TestEmailsByUserId.GetValueOrDefault(userId)
            ?? throw new InvalidOperationException(
                $"No test email registered for user id {userId}. Create the user via {nameof(CreateUserForTestAsync)} first.");
        var req = new LoginRequestDto
        {
            Email = email,
            Password = password,
        };
        var login = await client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(login, HttpStatusCode.OK);

        var loginRes = await login.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(loginRes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }
}
