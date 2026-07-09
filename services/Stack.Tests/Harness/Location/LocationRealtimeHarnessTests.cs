using System.Net;
using System.Net.Http.Json;
using Location.Dto;
using Location.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using Stack.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Users.Dto;

namespace Stack.Tests.Harness.Location;

[Collection(HarnessTestCollection.Name)]
[Trait(HarnessTraits.Category, HarnessTraits.Harness)]
[Trait(HarnessTraits.HarnessModule, HarnessTraits.Location)]
public sealed class LocationRealtimeHarnessTests : HarnessTestBase
{
    [Fact]
    public async Task UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(UpdatePosition_PushesLocationUpdated_ToJoinedGroupMember);

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var member = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        var group = await GroupHarnessHelpers.CreateGroupAsync(Client, owner, global::Group.Entities.GroupVisibility.Public, global::Group.Entities.GroupJoinPolicy.Open);
        await AddGroupMemberAsync(owner, group.Id, member);
        var session = await LocationHarnessHelpers.StartSessionAsync(Client, owner, group.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, member);
        var hubConnection = await HarnessRealtimeTestHelpers.ConnectAndJoinAsync(
            Client, "hubs/location", "JoinSession", session.Id);
        var waitForUpdate = HarnessRealtimeTestHelpers.WaitForHubEventAsync<LiveLocationGetResponseDto>(
            hubConnection, LocationHub.LocationUpdatedEvent);

        await HarnessAuthHelpers.LoginAsAsync(Client, owner);
        var updateRes = await Client.PatchAsJsonAsync(
            $"{LocationHarnessHelpers.SessionsBase}/{session.Id}/position",
            new LocationPositionUpdateRequestDto { Latitude = 37.6m, Longitude = 127.0m },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(updateRes, HttpStatusCode.OK);
        var pushed = await waitForUpdate;
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

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var stranger = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        var group = await GroupHarnessHelpers.CreateGroupAsync(Client, owner, global::Group.Entities.GroupVisibility.Public, global::Group.Entities.GroupJoinPolicy.Open);
        var session = await LocationHarnessHelpers.StartSessionAsync(Client, owner, group.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, stranger);
        var hubConnection = LocationHarnessHelpers.BuildHubConnection(Client);
        await hubConnection.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            hubConnection.InvokeAsync("JoinSession", session.Id, TestContext.Current.CancellationToken));

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task TriggerSos_PushesSafetyAlertRaised_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(TriggerSos_PushesSafetyAlertRaised_ToJoinedGroupMember);

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var member = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        var group = await GroupHarnessHelpers.CreateGroupAsync(Client, owner, global::Group.Entities.GroupVisibility.Public, global::Group.Entities.GroupJoinPolicy.Open);
        await AddGroupMemberAsync(owner, group.Id, member);
        var session = await LocationHarnessHelpers.StartSessionAsync(Client, owner, group.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, member);
        var hubConnection = await HarnessRealtimeTestHelpers.ConnectAndJoinAsync(
            Client, "hubs/location", "JoinGroupAlerts", group.Id);
        var waitForAlert = HarnessRealtimeTestHelpers.WaitForHubEventAsync<LocationSafetyAlertDto>(
            hubConnection, LocationHub.SafetyAlertRaisedEvent);

        await HarnessAuthHelpers.LoginAsAsync(Client, owner);
        var sosRes = await Client.PostAsync(
            $"{LocationHarnessHelpers.SessionsBase}/{session.Id}/sos",
            null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(sosRes, HttpStatusCode.OK);
        var pushed = await waitForAlert;
        Assert.Equal(LocationSafetyAlertType.Sos, pushed.Type);
        Assert.Equal(group.Id, pushed.GroupId);
        Assert.Equal(session.Id, pushed.SessionId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task StopSession_PushesLocationSessionEnded_ToJoinedGroupMember()
    {
        const string testMethodName = nameof(StopSession_PushesLocationSessionEnded_ToJoinedGroupMember);

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        var member = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 2);
        var group = await GroupHarnessHelpers.CreateGroupAsync(Client, owner, global::Group.Entities.GroupVisibility.Public, global::Group.Entities.GroupJoinPolicy.Open);
        await AddGroupMemberAsync(owner, group.Id, member);
        var session = await LocationHarnessHelpers.StartSessionAsync(Client, owner, group.Id);

        await HarnessAuthHelpers.LoginAsAsync(Client, member);
        var hubConnection = await HarnessRealtimeTestHelpers.ConnectAndJoinAsync(
            Client, "hubs/location", "JoinSession", session.Id);
        var waitForEnded = HarnessRealtimeTestHelpers.WaitForHubEventAsync<LocationSessionEndedDto>(
            hubConnection, LocationHub.LocationSessionEndedEvent);

        await HarnessAuthHelpers.LoginAsAsync(Client, owner);
        var stopRes = await Client.DeleteAsync($"{LocationHarnessHelpers.SessionsBase}/{session.Id}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(stopRes, HttpStatusCode.NoContent);
        var pushed = await waitForEnded;
        Assert.Equal(session.Id, pushed.SessionId);
        Assert.Equal(group.Id, pushed.GroupId);
        Assert.Equal(owner.Id, pushed.UserId);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task CreateMapPin_ClusterWorker_ReturnsClusters()
    {
        const string testMethodName = nameof(CreateMapPin_ClusterWorker_ReturnsClusters);
        const decimal seoulLat = 37.5665m;
        const decimal seoulLng = 126.9780m;

        var owner = await HarnessAuthHelpers.CreateUserForTestAsync(Client, UniqueTestPrefix(testMethodName), index: 1);
        await LocationHarnessHelpers.CreateMapPinAsync(Client, owner, seoulLat, seoulLng);

        await HarnessAuthHelpers.LoginAsAsync(Client, owner);
        var clusters = await LocationHarnessHelpers.PollClustersUntilReadyAsync(
            Client,
            minLatitude: 37m,
            maxLatitude: 38m,
            minLongitude: 126m,
            maxLongitude: 127m,
            zoom: 3,
            timeout: TimeSpan.FromSeconds(60));

        Assert.Contains(clusters, c => c.PinCount >= 1);
    }

    private async Task AddGroupMemberAsync(UserGetResponseDto owner, long groupId, UserGetResponseDto member)
    {
        var invitation = await GroupHarnessHelpers.InviteUserAsync(Client, owner, groupId, member.Id);
        await GroupHarnessHelpers.AcceptInvitationAsync(Client, member, invitation.Id);
    }
}
