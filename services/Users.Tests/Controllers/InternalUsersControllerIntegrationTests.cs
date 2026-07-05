using System.Net;
using System.Net.Http.Json;
using Users.Domain;
using Users.Dto;
using Users.Tests.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class InternalUsersControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    [Fact]
    public async Task InternalEndpoint_Returns401_WhenSecretMissing()
    {
        GatewayTestAuthHelpers.ClearAuth(Client);

        var res = await Client.PostAsync(
            "/internal/users/1/exists",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InternalEndpoint_Returns401_WhenSecretWrong()
    {
        GatewayTestAuthHelpers.ClearAuth(Client);
        Client.DefaultRequestHeaders.Add("X-Internal-Secret", "wrong-secret");

        var res = await Client.PostAsync(
            "/internal/users/1/exists",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EnsureUserExists_Returns400_WithAuthenticationFailed_WhenMissing()
    {
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsync(
            "/internal/users/999/exists",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "Authentication failed");
    }

    [Fact]
    public async Task ValidateUserExists_Returns400_WithUserNotFound_WhenMissing()
    {
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsync(
            "/internal/users/999/validate",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "User not found");
    }

    [Fact]
    public async Task EnsureUserExists_Returns204_WhenUserExists()
    {
        const string testMethodName = nameof(EnsureUserExists_Returns204_WhenUserExists);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsync(
            $"/internal/users/{user.Id}/exists",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EnsureUsersExist_Returns400_WhenAnyUserMissing()
    {
        const string testMethodName = nameof(EnsureUsersExist_Returns400_WhenAnyUserMissing);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsJsonAsync(
            "/internal/users/validate-exist",
            new InternalUsersUserIdsRequestDto([user.Id, 999]),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "User not found");
    }

    [Fact]
    public async Task GetNicknames_ReturnsDeletedUserFallback_ForMissingIds()
    {
        const string testMethodName = nameof(GetNicknames_ReturnsDeletedUserFallback_ForMissingIds);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsJsonAsync(
            "/internal/users/nicknames",
            new InternalUsersUserIdsRequestDto([user.Id, 999]),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalUsersNicknamesResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(2, body.Nicknames.Count);
        Assert.Equal(user.Nickname, body.Nicknames.Single(n => n.UserId == user.Id).Nickname);
        Assert.Equal("Deleted User", body.Nicknames.Single(n => n.UserId == 999).Nickname);
    }

    [Fact]
    public async Task GetUserIdByNickname_Returns404_WhenMissing()
    {
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsJsonAsync(
            "/internal/users/by-nickname",
            new InternalUsersNicknameLookupRequestDto("no-such-nickname"),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserIdByNickname_ReturnsUserId_WhenFound()
    {
        const string testMethodName = nameof(GetUserIdByNickname_ReturnsUserId_WhenFound);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsJsonAsync(
            "/internal/users/by-nickname",
            new InternalUsersNicknameLookupRequestDto(user.Nickname),
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalUsersNicknameLookupResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.UserId);
    }

    [Fact]
    public async Task GetFriendsListVisibility_ReturnsSetting()
    {
        const string testMethodName = nameof(GetFriendsListVisibility_ReturnsSetting);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsync(
            $"/internal/users/{user.Id}/friends-list-visibility",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<InternalUsersFriendsListVisibilityResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(FriendsListVisibility.Private, body.FriendsListVisibility);
    }
}
