using System.Net;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class PlaceControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task ReverseGeocode_Returns400_WhenLatitudeOutOfRange()
    {
        // Act
        var res = await Client.GetAsync(
            "/api/location/places/reverse?latitude=95&longitude=0",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }
}
