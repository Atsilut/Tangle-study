using Api.Domain.Location.Dto;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class MapPinServiceUnitTests
{
    [Fact]
    public async Task CreateMapPinAsync_ValidRequest_CreatesPin()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);

        // Act
        var dto = await graph.MapPinService.CreateMapPinAsync(new MapPinCreateRequestDto
        {
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        // Assert
        Assert.Equal(user.Id, dto.OwnerUserId);
        Assert.Equal(37.5665m, dto.Latitude);
    }

    [Fact]
    public async Task DeleteMapPinByIdAsync_NonOwner_ThrowsUnauthorized()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var other = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "other");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var created = await graph.MapPinService.CreateMapPinAsync(new MapPinCreateRequestDto
        {
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        http.HttpContext = ServiceTestHelpers.ContextFor(other.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.MapPinService.DeleteMapPinByIdAsync(created.Id));
    }

    [Fact]
    public async Task GetMapPinByIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var graph = LocationServiceTestFactory.Create();

        // Act
        var dto = await graph.MapPinService.GetMapPinByIdAsync(99999);

        // Assert
        Assert.Null(dto);
    }
}
