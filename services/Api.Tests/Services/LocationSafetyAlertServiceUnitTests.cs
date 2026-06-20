using Api.Domain.Location.Dto;
using Api.Domain.Location.Storage;
using Api.Domain.UserBlocks.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class LocationSafetyAlertServiceUnitTests
{
    [Fact]
    public async Task TriggerSosAsync_NotifiesGroupMembers()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Helper");
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
        var alert = await graph.SafetyAlertService.TriggerSosAsync(started.Id);

        // Assert
        Assert.Equal(LocationSafetyAlertType.Sos, alert.Type);
        Assert.Equal(groupId, alert.GroupId);
        Assert.NotNull(graph.RealtimeNotifier.LastSafetyAlert);
        Assert.Equal(LocationSafetyAlertType.Sos, graph.RealtimeNotifier.LastSafetyAlert!.Type);
    }

    [Fact]
    public async Task EvaluateStaleSessionsAsync_SendsStaleAlertOnce()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Walker");
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

        await graph.LiveStore.SetLiveLocationAsync(new LiveLocationSnapshot(
            started.Id,
            groupId,
            user.Id,
            37.5m,
            126.9m,
            DateTime.UtcNow.AddMinutes(-5)));

        // Act
        await graph.SafetyAlertService.EvaluateStaleSessionsAsync();
        await graph.SafetyAlertService.EvaluateStaleSessionsAsync();

        // Assert
        Assert.Equal(1, graph.RealtimeNotifier.SafetyAlertCount);
        Assert.NotNull(graph.RealtimeNotifier.LastSafetyAlert);
        Assert.Equal(LocationSafetyAlertType.StalePosition, graph.RealtimeNotifier.LastSafetyAlert!.Type);
        Assert.Equal(started.Id, graph.RealtimeNotifier.LastSafetyAlert.SessionId);
    }

    [Fact]
    public async Task TriggerSosAsync_SecondCallWithinCooldown_ThrowsArgumentException()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Helper");
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

        await graph.SafetyAlertService.TriggerSosAsync(started.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            graph.SafetyAlertService.TriggerSosAsync(started.Id));
    }

    [Fact]
    public async Task TriggerSosAsync_ExcludesBlockedRecipients()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = LocationSessionServiceTestFactory.Create(http);
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Owner");
        var member = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Member");
        var blocked = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, nickname: "Blocked");
        var groupId = await LocationSessionServiceTestFactory.SeedGroupWithMembersAsync(
            graph.GroupMemberRepository,
            owner.Id,
            member.Id,
            blocked.Id);

        http.HttpContext = ServiceTestHelpers.ContextFor(blocked.Id);
        await graph.UserBlockRepository.CreateUserBlockAsync(new UserBlock(blocked.Id, owner.Id));

        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        var started = await graph.LocationSessionService.StartSessionAsync(new LocationSessionCreateRequestDto
        {
            GroupId = groupId,
            Latitude = 37.5m,
            Longitude = 126.9m,
        });

        // Act
        await graph.SafetyAlertService.TriggerSosAsync(started.Id);

        // Assert
        Assert.Equal([member.Id], graph.RealtimeNotifier.LastSafetyAlertRecipients);
    }
}
