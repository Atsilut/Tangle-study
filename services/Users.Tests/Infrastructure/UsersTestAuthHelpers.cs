using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;
using Users.Dto;

namespace Users.Tests.Infrastructure;

public static class UsersTestAuthHelpers
{
    public const string DefaultPassword = "testtest123!";

    private static readonly ConcurrentDictionary<long, string> TestEmailsByUserId = new();

    public static void RegisterTestEmail(long userId, string email) =>
        TestEmailsByUserId[userId] = email;

    public static string GetTestEmail(long userId) =>
        TestEmailsByUserId.GetValueOrDefault(userId)
        ?? throw new InvalidOperationException(
            $"No test email registered for user id {userId}. Create the user via {nameof(CreateUserForTestAsync)} first.");

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
        RegisterTestEmail(profile.Id, email);
        return profile;
    }

    public static Task LoginAsAsync(HttpClient client, UserGetResponseDto user)
    {
        GatewayTestAuthHelpers.LoginAs(client, user.Id);
        return Task.CompletedTask;
    }
}
