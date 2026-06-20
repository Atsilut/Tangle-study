using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class LocationClusterServiceUnitTests
{
    [Fact]
    public async Task StoreClustersAsync_ThenGetClustersAsync_ReturnsStoredClusters()
    {
        // Arrange
        var graph = LocationServiceTestFactory.Create();
        var query = new MapClusterBoundsQueryDto
        {
            MinLatitude = 37m,
            MaxLatitude = 38m,
            MinLongitude = 126m,
            MaxLongitude = 127m,
            Zoom = 4,
        };
        var clusters = new List<MapClusterGetResponseDto>
        {
            new(37.5m, 126.9m, 2, 1),
        };

        // Act
        await graph.LocationClusterService.StoreClustersAsync(new LocationClusterStoreRequestDto
        {
            MinLatitude = query.MinLatitude,
            MaxLatitude = query.MaxLatitude,
            MinLongitude = query.MinLongitude,
            MaxLongitude = query.MaxLongitude,
            Zoom = query.Zoom,
            Clusters = clusters,
        });
        var result = await graph.LocationClusterService.GetClustersAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2, result[0].PinCount);
    }

    [Fact]
    public async Task GetClusterPointsInBoundsAsync_ReturnsVisiblePins()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        await graph.MapPinService.CreateMapPinAsync(new MapPinCreateRequestDto
        {
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        // Act
        var points = await graph.LocationClusterService.GetClusterPointsInBoundsAsync(
            new MapPinBoundsQueryDto
            {
                MinLatitude = 37m,
                MaxLatitude = 38m,
                MinLongitude = 126m,
                MaxLongitude = 127m,
            });

        // Assert
        Assert.NotNull(points);
        Assert.Single(points);
    }
}
