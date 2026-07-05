using System.Net;
using System.Net.Http.Json;
using Users.Dto;
using Users.Tests.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class GatewayIdentityIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenGatewaySecretWrong()
    {
        const string testMethodName = nameof(ProtectedEndpoint_Returns401_WhenGatewaySecretWrong);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.ClearAuth(Client);
        Client.DefaultRequestHeaders.Add("X-Gateway-Secret", "wrong-gateway-secret");
        Client.DefaultRequestHeaders.Add("X-User-Id", user.Id.ToString());

        var res = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = user.Id, Nickname = "new" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenUserIdMissing()
    {
        const string testMethodName = nameof(ProtectedEndpoint_Returns401_WhenUserIdMissing);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.ClearAuth(Client);
        Client.DefaultRequestHeaders.Add("X-Gateway-Secret", GatewayTestAuthHelpers.TestGatewaySecret);

        var res = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = user.Id, Nickname = "new" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WhenUserWasDeleted()
    {
        const string testMethodName = nameof(ProtectedEndpoint_Returns401_WhenUserWasDeleted);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var delete = await Client.DeleteAsync($"/api/users/{user.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        var res = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = user.Id, Nickname = "ghost" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }
}
