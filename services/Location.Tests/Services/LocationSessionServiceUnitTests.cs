using Location.Dto;
using Location.Tests.Infrastructure;

namespace Location.Tests.Services;

public sealed class LocationSessionServiceUnitTests
{
    [Fact]
    public async Task StartSessionAsync_ThenGetMyActiveSessionAsync_ReturnsSession()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
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
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
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
        var sharer = ServiceTestHelpers.CreateUser(graph.InMemoryUser,"Sharer");
        var viewer = ServiceTestHelpers.CreateUser(graph.InMemoryUser,"Viewer");
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
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
    public async Task GetGroupMemberSharingStatusAsync_ReturnsSharingAndNotSharingMembers()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var sharer = ServiceTestHelpers.CreateUser(graph.InMemoryUser,"Sharer");
        var idle = ServiceTestHelpers.CreateUser(graph.InMemoryUser,"Idle");
        var viewer = ServiceTestHelpers.CreateUser(graph.InMemoryUser,"Viewer");
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
            sharer.Id,
            idle.Id,
            viewer.Id);

        http.HttpContext = ServiceTestHelpers.ContextFor(sharer.Id);
        await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });

        http.HttpContext = ServiceTestHelpers.ContextFor(viewer.Id);

        // Act
        var status = await graph.LocationSessionService.GetGroupMemberSharingStatusAsync(groupId);

        // Assert
        Assert.Equal(2, status.Count);
        Assert.Contains(status, s => s.UserNickname == "Sharer" && s.IsSharing);
        Assert.Contains(status, s => s.UserNickname == "Idle" && !s.IsSharing);
        Assert.DoesNotContain(status, s => s.UserNickname == "Viewer");
    }

    [Fact]
    public async Task StopSessionAsync_ClearsActiveSession()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
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

    [Fact]
    public async Task StopSessionAsync_NotifiesSessionEnded()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
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

        // Assert
        Assert.NotNull(graph.RealtimeNotifier.LastSessionEnded);
        Assert.Equal(started.Id, graph.RealtimeNotifier.LastSessionEnded!.SessionId);
        Assert.Equal(groupId, graph.RealtimeNotifier.LastSessionEnded.GroupId);
        Assert.Equal(user.Id, graph.RealtimeNotifier.LastSessionEnded.UserId);
    }

    [Fact]
    public async Task ReconcileGhostSessionsAsync_EndsSession_WhenRedisMissing()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        await graph.LiveStore.RemoveLiveLocationAsync(groupId, user.Id);

        // Act
        await graph.LocationSessionService.ReconcileGhostSessionsAsync();
        var mine = await graph.LocationSessionService.GetMyActiveSessionAsync(groupId);

        // Assert
        Assert.Null(mine);
    }

    [Fact]
    public async Task HandleUserDeletionAsync_PurgesRedisLiveLocation()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        Assert.NotNull(await graph.LiveStore.GetLiveLocationAsync(groupId, user.Id));

        // Act
        await graph.LocationSessionService.HandleUserDeletionAsync(user.Id);

        // Assert
        Assert.Null(await graph.LiveStore.GetLiveLocationAsync(groupId, user.Id));
    }

    [Fact]
    public async Task GetMyActiveSessionAsync_EndsGhostSession_WhenRedisMissing()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = ServiceTestHelpers.CreateUser(graph.InMemoryUser);
        var groupId = LocationSessionServiceTestFactory.SeedGroupWithMembers(
            graph.FakeGroup,
            user.Id);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);
        await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5665m,
            Longitude = 126.9780m,
        });
        await graph.LiveStore.RemoveLiveLocationAsync(groupId, user.Id);

        // Act
        var mine = await graph.LocationSessionService.GetMyActiveSessionAsync(groupId);

        // Assert
        Assert.Null(mine);
    }
}
