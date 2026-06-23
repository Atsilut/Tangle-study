using System.Net;
using System.Net.Http.Json;
using Api.Domain.Location.Dto;
using Api.Domain.Posts.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class InternalLocationControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
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
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var postRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "cluster post", Content = "content" },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var posts = await Client.GetFromJsonAsync<List<PostGetResponseDto>>("/api/posts", TestContext.Current.CancellationToken);
        var post = posts!.Single();
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = TestLat, Longitude = TestLng, PostId = post.Id },
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
