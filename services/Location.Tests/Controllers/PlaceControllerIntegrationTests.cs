using System.Net;
using Location.Tests.Infrastructure;

namespace Location.Tests.Controllers;

[Collection(LocationIntegrationTestCollection.Name)]
public sealed class PlaceControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : LocationIntegrationTestBase(postgres, redis)
{
    [Fact]
    public async Task SearchPlaces_Returns401_WhenAnonymous()
    {
        var res = await Client.GetAsync(
            "/api/location/places/search?q=Seoul",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReverseGeocode_Returns401_WhenAnonymous()
    {
        var res = await Client.GetAsync(
            "/api/location/places/reverse?latitude=37.5665&longitude=126.978",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReverseGeocode_Returns400_WhenLatitudeOutOfRange()
    {
        var user = CreateUserForTest(nameof(ReverseGeocode_Returns400_WhenLatitudeOutOfRange));
        LoginAs(user);

        var res = await Client.GetAsync(
            "/api/location/places/reverse?latitude=95&longitude=0",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }
}
