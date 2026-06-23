using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Location.Realtime;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class LocationRedisRealtimeIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : FriendshipDomainIntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    private const string SessionsBase = "/api/location/sessions";

    [Fact]
    public async Task UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(member);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LiveLocationGetResponseDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, tokenB);
        hubConnection.On<LiveLocationGetResponseDto>(LocationHub.LocationUpdatedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken);

        // Act
        await LoginAs(owner);
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
        Assert.Equal(group.Id, pushed.GroupId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task JoinSession_ThrowsHubException_WhenCallerIsNotGroupMember()
    {
        const string testMethodName = nameof(JoinSession_ThrowsHubException_WhenCallerIsNotGroupMember);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var stranger = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(stranger);
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
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(member);
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LocationSafetyAlertDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, token);
        hubConnection.On<LocationSafetyAlertDto>(LocationHub.SafetyAlertRaisedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinGroupAlerts", group.Id, TestContext.Current.CancellationToken);

        // Act
        await LoginAs(owner);
        var sosRes = await Client.PostAsync(
            $"{SessionsBase}/{session.Id}/sos",
            null,
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(sosRes, HttpStatusCode.OK);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(LocationSafetyAlertType.Sos, pushed.Type);
        Assert.Equal(group.Id, pushed.GroupId);
        Assert.Equal(session.Id, pushed.SessionId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task StopSession_PushesLocationSessionEnded_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(StopSession_PushesLocationSessionEnded_ToJoinedGroupMember);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = 37.5m,
                Longitude = 126.9m,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(startRes, HttpStatusCode.Created);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(member);
        var tokenB = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var received = new TaskCompletionSource<LocationSessionEndedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hubConnection = LocationRealtimeTestHelpers.BuildHubConnection(Factory, Client, tokenB);
        hubConnection.On<LocationSessionEndedDto>(LocationHub.LocationSessionEndedEvent, dto => received.TrySetResult(dto));
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);
        await hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken);

        // Act
        await LoginAs(owner);
        var stopRes = await Client.DeleteAsync($"{SessionsBase}/{session.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(stopRes, HttpStatusCode.NoContent);
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(session.Id, pushed.SessionId);
        Assert.Equal(group.Id, pushed.GroupId);
        Assert.Equal(owner.Id, pushed.UserId);

        await hubConnection.DisposeAsync();
    }
}
