using Location.Dto;
using Location.Tests.Infrastructure;

namespace Location.Tests.Services;

public sealed class MapPinServiceUnitTests
{
    [Fact]
    public async Task CreateMapPinAsync_ValidRequest_CreatesPin()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationServiceTestFactory.Create(http);
        var user = graph.InMemoryUser.CreateUser("user");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(user.Id);

        var dto = await graph.MapPinService.CreateMapPinAsync(new MapPinCreateRequestDto
        {
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        Assert.Equal(user.Id, dto.OwnerUserId);
        Assert.Equal(37.5665m, dto.Latitude);
    }

    [Fact]
    public async Task DeleteMapPinByIdAsync_NonOwner_ThrowsUnauthorized()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationServiceTestFactory.Create(http);
        var owner = graph.InMemoryUser.CreateUser("owner");
        var other = graph.InMemoryUser.CreateUser("other");
        http.HttpContext = FakeHttpContextAccessor.ContextFor(owner.Id);
        var created = await graph.MapPinService.CreateMapPinAsync(new MapPinCreateRequestDto
        {
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        http.HttpContext = FakeHttpContextAccessor.ContextFor(other.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.MapPinService.DeleteMapPinByIdAsync(created.Id));
    }

    [Fact]
    public async Task GetMapPinByIdAsync_ReturnsNull_WhenMissing()
    {
        var graph = LocationServiceTestFactory.Create();

        var dto = await graph.MapPinService.GetMapPinByIdAsync(99999);

        Assert.Null(dto);
    }
}
