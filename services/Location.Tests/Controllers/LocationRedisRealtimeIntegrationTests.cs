using System.Net;
using System.Net.Http.Json;
using Location.Dto;
using Location.Realtime;
using Location.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Location.Tests.Controllers;

[Collection(LocationIntegrationTestCollection.Name)]
public sealed class LocationRedisRealtimeIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : LocationIntegrationTestBase(postgres, redis)
{
    private const string SessionsBase = "/api/location/sessions";

    [Fact]
    public async Task UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember);

        // Arrange
        var owner = CreateUserForTest(testMethodName, 1);
        var member = CreateUserForTest(testMethodName, 2);
        var groupId = CreateGroupWithOwner(owner);
        AddGroupMember(groupId, member);

        LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = groupId,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        LoginAs(member);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LiveLocationGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, tokenB);
        hubConnection.On<LiveLocationGetResponseDto>(LocationHub.LocationUpdatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken);

        // Act
        LoginAs(owner);
        var updateRes = await Client.PatchAsJsonAsync(
            $"{SessionsBase}/{session.Id}/position",
            new LocationPositionUpdateRequestDto { Latitude = 37.6m, Longitude = 127.0m },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(updateRes, HttpStatusCode.OK);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(37.6m, pushed.Latitude);
        Assert.Equal(127.0m, pushed.Longitude);
        Assert.Equal(session.Id, pushed.SessionId);
        Assert.Equal(groupId, pushed.GroupId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinSession_ThrowsHubException_WhenCallerIsNotGroupMember()
    {
        const string testMethodName = nameof(JoinSession_ThrowsHubException_WhenCallerIsNotGroupMember);

        // Arrange
        var owner = CreateUserForTest(testMethodName, 1);
        var stranger = CreateUserForTest(testMethodName, 2);
        var groupId = CreateGroupWithOwner(owner);

        LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = groupId,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        LoginAs(stranger);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() =>
            hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken));

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task TriggerSos_PushesSafetyAlertRaised_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(TriggerSos_PushesSafetyAlertRaised_ToJoinedGroupMember);

        // Arrange
        var owner = CreateUserForTest(testMethodName, 1);
        var member = CreateUserForTest(testMethodName, 2);
        var groupId = CreateGroupWithOwner(owner);
        AddGroupMember(groupId, member);

        LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = groupId,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        LoginAs(member);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LocationSafetyAlertDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        hubConnection.On<LocationSafetyAlertDto>(LocationHub.SafetyAlertRaisedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinGroupAlerts", groupId, TestContext.Current.CancellationToken);

        // Act
        LoginAs(owner);
        var sosRes = await Client.PostAsync(
            $"{SessionsBase}/{session.Id}/sos",
            null,
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(sosRes, HttpStatusCode.OK);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(LocationSafetyAlertType.Sos, pushed.Type);
        Assert.Equal(groupId, pushed.GroupId);
        Assert.Equal(session.Id, pushed.SessionId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task StopSession_PushesLocationSessionEnded_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(StopSession_PushesLocationSessionEnded_ToJoinedGroupMember);

        // Arrange
        var owner = CreateUserForTest(testMethodName, 1);
        var member = CreateUserForTest(testMethodName, 2);
        var groupId = CreateGroupWithOwner(owner);
        AddGroupMember(groupId, member);

        LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = groupId,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        LoginAs(member);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LocationSessionEndedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, tokenB);
        hubConnection.On<LocationSessionEndedDto>(LocationHub.LocationSessionEndedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken);

        // Act
        LoginAs(owner);
        var stopRes = await Client.DeleteAsync($"{SessionsBase}/{session.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(stopRes, HttpStatusCode.NoContent);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(session.Id, pushed.SessionId);
        Assert.Equal(groupId, pushed.GroupId);
        Assert.Equal(owner.Id, pushed.UserId);

        await hubConnection.DisposeAsync();
    }
}
