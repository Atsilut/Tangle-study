using System.Net;
using Api.Domain.Location.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class LocationClusterControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetClusters_Returns401_WhenUnauthenticated()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var res = await Client.GetAsync(
            "/api/location/clusters?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127&zoom=3",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClusters_Returns204_WhenNoCachedClusters()
    {
        // Arrange
        const string testMethodName = nameof(GetClusters_Returns204_WhenNoCachedClusters);
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);

        // Act
        var res = await Client.GetAsync(
            "/api/location/clusters?minLatitude=0&maxLatitude=1&minLongitude=0&maxLongitude=1&zoom=3",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }
}
