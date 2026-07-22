using System.Net;
using System.Net.Http.Json;
using Location.Dto;
using Location.Tests.Infrastructure;
using Tangle.AspNetCore.Security;
using Tangle.TestSupport.Auth;

namespace Location.Tests.Controllers;

[Collection(LocationIntegrationTestCollection.Name)]
public sealed class InternalLocationControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : LocationIntegrationTestBase(postgres, redis)
{
    private const decimal TestLat = 37.5665m;
    private const decimal TestLng = 126.9780m;

    [Fact]
    public async Task GetClusterPoints_Returns401_WithoutWorkerSecret()
    {
        // Arrange
        var query = BuildBoundsQuery();

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Get,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/cluster-points?{LocationIntegrationTestHelpers.BuildClusterPointsQuery(query)}",
            workerSecret: null);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClusterPoints_Returns401_WithInvalidWorkerSecret()
    {
        // Arrange
        var query = BuildBoundsQuery();

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Get,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/cluster-points?{LocationIntegrationTestHelpers.BuildClusterPointsQuery(query)}",
            workerSecret: "wrong-secret");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClusterPoints_Returns204_WhenNoPins()
    {
        // Arrange
        var query = new MapPinBoundsQueryDto
        {
            MinLatitude = 0,
            MaxLatitude = 1,
            MinLongitude = 0,
            MaxLongitude = 1,
        };

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Get,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/cluster-points?{LocationIntegrationTestHelpers.BuildClusterPointsQuery(query)}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetClusterPoints_Returns200_WithVisiblePin()
    {
        // Arrange
        const string testMethodName = nameof(GetClusterPoints_Returns200_WithVisiblePin);
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var postId = FakeCommunity.SeedPost(user.Id);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = TestLat, Longitude = TestLng, PostId = postId },
            TestContext.Current.CancellationToken);

        var query = BuildBoundsQuery();

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Get,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/cluster-points?{LocationIntegrationTestHelpers.BuildClusterPointsQuery(query)}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var points = await res.Content.ReadFromJsonAsync<List<MapPinClusterPointDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(points);
        Assert.Single(points!);
        Assert.Equal(TestLat, points[0].Latitude);
    }

    [Fact]
    public async Task StoreClusters_Returns401_WithoutWorkerSecret()
    {
        // Arrange
        var request = BuildStoreRequest();

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Put,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/clusters",
            request,
            workerSecret: null);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StoreClusters_Returns204_WithValidWorkerSecret()
    {
        // Arrange
        var request = BuildStoreRequest();

        // Act
        var res = await LocationIntegrationTestHelpers.SendWorkerRequestAsync(
            Client,
            HttpMethod.Put,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/clusters",
            request);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EndSessionsForGroup_IsIdempotent_WhenCalledTwice()
    {
        using var first = new HttpRequestMessage(
            HttpMethod.Post,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/groups/999/end-sessions");
        first.Headers.Add(
            InternalAccessAuthorizationFilter.HeaderName,
            GatewayTestAuthHelpers.TestInternalServiceSecret);
        var firstRes = await Client.SendAsync(first, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(firstRes, HttpStatusCode.NoContent);

        using var second = new HttpRequestMessage(
            HttpMethod.Post,
            $"{LocationIntegrationTestHelpers.InternalLocationBase}/groups/999/end-sessions");
        second.Headers.Add(
            InternalAccessAuthorizationFilter.HeaderName,
            GatewayTestAuthHelpers.TestInternalServiceSecret);
        var secondRes = await Client.SendAsync(second, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(secondRes, HttpStatusCode.NoContent);
    }

    private static MapPinBoundsQueryDto BuildBoundsQuery() =>
        new()
        {
            MinLatitude = 37,
            MaxLatitude = 38,
            MinLongitude = 126,
            MaxLongitude = 127,
        };

    private static LocationClusterStoreRequestDto BuildStoreRequest() =>
        new()
        {
            MinLatitude = 37,
            MaxLatitude = 38,
            MinLongitude = 126,
            MaxLongitude = 127,
            Zoom = LocationClusterRules.MinZoom,
            Clusters =
            [
                new MapClusterGetResponseDto(TestLat, TestLng, 1, null),
            ],
        };
}
