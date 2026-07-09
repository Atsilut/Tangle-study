using System.Net;
using System.Net.Http.Json;
using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Gateway;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Users)]
public sealed class GatewayProtectedRouteHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task ProtectedRoute_WithoutJwt_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = 1, Nickname = "noauth" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }
}

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Community)]
public sealed class GatewayAnonymousReadHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task GetPostsWithoutJwt_DoesNotRequireAuth()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Expected 200 or 204 for anonymous GET /api/posts, got {(int)response.StatusCode}.");
    }
}
