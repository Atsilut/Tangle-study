using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Users.Dto;

namespace Users.Tests.Infrastructure;

internal static class IntegrationTestAuthHelpers
{
    public const string DefaultPassword = "testtest123!";

    private static readonly ConcurrentDictionary<long, string> TestEmailsByUserId = new();

    internal static void RegisterTestEmail(long userId, string email) =>
        TestEmailsByUserId[userId] = email;

    internal static string GetTestEmail(long userId) =>
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
        var nickname = $"{testMethodName}User" + index.ToString();
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

    public static Task LoginAsAsync(HttpClient client, UserGetResponseDto user, string? password = null)
    {
        UsersTestAuthHelpers.LoginAs(client, user.Id);
        return Task.CompletedTask;
    }
}
