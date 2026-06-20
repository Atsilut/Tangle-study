using Api.Domain.Location.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class LocationSessionServiceUnitTests
{
    [Fact]
    public async Task StartSessionAsync_ThenGetMyActiveSessionAsync_ReturnsSession()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        var groupId = await LocationSessionServiceTestFactory.SeedGroupWithMembersAsync(
            graph.GroupMemberRepository,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);

        // Act
        var started = await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        var mine = await graph.LocationSessionService.GetMyActiveSessionAsync(groupId);

        // Assert
        Assert.NotNull(mine);
        Assert.Equal(started.Id, mine!.Id);
        Assert.Equal(groupId, mine.GroupId);
        Assert.Equal(37.5665m, mine.Latitude);
    }

    [Fact]
    public async Task UpdatePositionAsync_NotifiesRealtimeClients()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        var groupId = await LocationSessionServiceTestFactory.SeedGroupWithMembersAsync(
            graph.GroupMemberRepository,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        var started = await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5m,
            Longitude = 126.9m,
        });

        // Act
        await graph.LocationSessionService.UpdatePositionAsync(started.Id, new LocationPositionUpdateRequestDto
        {
            Latitude = 37.6m,
            Longitude = 127.0m,
        });

        // Assert
        Assert.NotNull(graph.RealtimeNotifier.LastNotification);
        Assert.Equal(37.6m, graph.RealtimeNotifier.LastNotification!.Latitude);
        Assert.Equal(started.Id, graph.RealtimeNotifier.LastNotification.SessionId);
        Assert.Equal(groupId, graph.RealtimeNotifier.LastNotification.GroupId);
    }

    [Fact]
    public async Task GetActiveGroupLocationsAsync_ReturnsOtherMemberSession()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var sharer = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Sharer");
        var viewer = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Viewer");
        var groupId = await LocationSessionServiceTestFactory.SeedGroupWithMembersAsync(
            graph.GroupMemberRepository,
            sharer.Id,
            viewer.Id);

        http.HttpContext = ServiceTestHelpers.ContextFor(sharer.Id);
        var started = await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        http.HttpContext = ServiceTestHelpers.ContextFor(viewer.Id);

        // Act
        var active = await graph.LocationSessionService.GetActiveGroupLocationsAsync(groupId);

        // Assert
        Assert.NotNull(active);
        Assert.Single(active!);
        Assert.Equal(started.Id, active![0].SessionId);
        Assert.Equal(groupId, active[0].GroupId);
        Assert.Equal("Sharer", active[0].UserNickname);
    }

    [Fact]
    public async Task StopSessionAsync_ClearsActiveSession()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        var groupId = await LocationSessionServiceTestFactory.SeedGroupWithMembersAsync(
            graph.GroupMemberRepository,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        var started = await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        // Act
        await graph.LocationSessionService.StopSessionAsync(started.Id);
        var mine = await graph.LocationSessionService.GetMyActiveSessionAsync(groupId);

        // Assert
        Assert.Null(mine);
    }
}
