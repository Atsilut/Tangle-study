using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;

namespace Api.Tests.Infrastructure;

internal static class IntegrationTestAuthHelpers
{
    public const string DefaultPassword = "testtest123!";

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
        return all!.Single(u => u.Email == req.Email);
    }

    public static async Task LoginAsAsync(HttpClient client, UserGetResponseDto user, string? password = null)
    {
        password ??= DefaultPassword;
        var req = new LoginRequestDto
        {
            Email = user.Email,
            Password = password,
        };
        var login = await client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(login, HttpStatusCode.OK);

        var loginRes = await login.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(loginRes);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }
}
