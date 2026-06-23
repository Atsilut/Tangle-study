using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Location.Storage;
using Api.Domain.UserBlocks.Dto;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Controllers;

[Collection(RedisRealtimeIntegrationTestCollection.Name)]
public sealed class LocationSessionControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : FriendshipDomainIntegrationTestBase(postgres, redisEnabled: true, redisConnectionString: redis.ConnectionString)
{
    private const decimal TestLat = 37.5665m;
    private const decimal TestLng = 126.9780m;

    [Fact]
    public async Task StartSession_Returns201_WhenGroupMember()
    {
        // Arrange
        const string testMethodName = nameof(StartSession_Returns201_WhenGroupMember);
        var owner = await CreateUserForTest(testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await LoginAs(owner);

        // Act
        var res = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var session = await res.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        Assert.Equal(group.Id, session.GroupId);
        Assert.Equal(owner.Id, session.UserId);
    }

    [Fact]
    public async Task GetMyActiveSession_Returns204_WhenNotSharing()
    {
        // Arrange
        const string testMethodName = nameof(GetMyActiveSession_Returns204_WhenNotSharing);
        var owner = await CreateUserForTest(testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await LoginAs(owner);

        // Act
        var res = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/mine?groupId={group.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StopSession_Returns204_ForOwner()
    {
        // Arrange
        const string testMethodName = nameof(StopSession_Returns204_ForOwner);
        var owner = await CreateUserForTest(testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        // Act
        var res = await Client.DeleteAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/{session.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        var mineRes = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/mine?groupId={group.Id}",
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(mineRes, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetActiveGroupLocations_Returns200_WithOtherMemberSession()
    {
        // Arrange
        const string testMethodName = nameof(GetActiveGroupLocations_Returns200_WithOtherMemberSession);
        var sharer = await CreateUserForTest(testMethodName, 1);
        var viewer = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, sharer);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, viewer.Id, GroupRole.Member);

        await LoginAs(sharer);
        var startRes = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(viewer);

        // Act
        var res = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/active?groupId={group.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var active = await res.Content.ReadFromJsonAsync<List<LiveLocationGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(active);
        Assert.Single(active!);
        Assert.Equal(session.Id, active[0].SessionId);
    }

    [Fact]
    public async Task GetGroupMemberSharingStatus_ReturnsSharingAndIdleMembers()
    {
        // Arrange
        const string testMethodName = nameof(GetGroupMemberSharingStatus_ReturnsSharingAndIdleMembers);
        var sharer = await CreateUserForTest(testMethodName, 1);
        var idle = await CreateUserForTest(testMethodName, 2);
        var viewer = await CreateUserForTest(testMethodName, 3);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, sharer);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, idle.Id, GroupRole.Member);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, viewer.Id, GroupRole.Member);

        await LoginAs(sharer);
        await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);

        await LoginAs(viewer);

        // Act
        var res = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/members?groupId={group.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var status = await res.Content.ReadFromJsonAsync<List<GroupMemberLocationStatusDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(status);
        Assert.Equal(2, status!.Count);
        Assert.Contains(status, s => s.UserNickname == sharer.Nickname && s.IsSharing);
        Assert.Contains(status, s => s.UserNickname == idle.Nickname && !s.IsSharing);
    }

    [Fact]
    public async Task StartSession_Returns404_WhenNotGroupMember()
    {
        // Arrange
        const string testMethodName = nameof(StartSession_Returns404_WhenNotGroupMember);
        var owner = await CreateUserForTest(testMethodName, 1);
        var stranger = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await LoginAs(stranger);

        // Act
        var res = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePosition_Returns401_WhenNonOwner()
    {
        // Arrange
        const string testMethodName = nameof(UpdatePosition_Returns401_WhenNonOwner);
        var owner = await CreateUserForTest(testMethodName, 1);
        var other = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, other.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(other);

        // Act
        var res = await Client.PatchAsJsonAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/{session.Id}/position",
            new LocationPositionUpdateRequestDto { Latitude = 37.6m, Longitude = 127.0m },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StopSession_Returns401_WhenNonOwner()
    {
        // Arrange
        const string testMethodName = nameof(StopSession_Returns401_WhenNonOwner);
        var owner = await CreateUserForTest(testMethodName, 1);
        var other = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, other.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(other);

        // Act
        var res = await Client.DeleteAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/{session.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TriggerSos_Returns401_WhenNonOwner()
    {
        // Arrange
        const string testMethodName = nameof(TriggerSos_Returns401_WhenNonOwner);
        var owner = await CreateUserForTest(testMethodName, 1);
        var other = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, other.Id, GroupRole.Member);

        await LoginAs(owner);
        var startRes = await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);
        var session = (await startRes.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(
            TestContext.Current.CancellationToken))!;

        await LoginAs(other);

        // Act
        var res = await Client.PostAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/{session.Id}/sos",
            null,
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActiveGroupLocations_Returns204_WhenBlockedMemberIsSharing()
    {
        // Arrange
        const string testMethodName = nameof(GetActiveGroupLocations_Returns204_WhenBlockedMemberIsSharing);
        var sharer = await CreateUserForTest(testMethodName, 1);
        var viewer = await CreateUserForTest(testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, sharer);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, viewer.Id, GroupRole.Member);

        await LoginAs(viewer);
        await Client.PostAsJsonAsync(
            "/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = sharer.Id },
            TestContext.Current.CancellationToken);

        await LoginAs(sharer);
        await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);

        await LoginAs(viewer);

        // Act
        var res = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/active?groupId={group.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMyActiveSession_Returns204_AndEndsGhostSession_WhenRedisMissing()
    {
        // Arrange
        const string testMethodName = nameof(GetMyActiveSession_Returns204_AndEndsGhostSession_WhenRedisMissing);
        var owner = await CreateUserForTest(testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await LoginAs(owner);
        await Client.PostAsJsonAsync(
            LocationIntegrationTestHelpers.SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = group.Id,
                Latitude = TestLat,
                Longitude = TestLng,
            },
            TestContext.Current.CancellationToken);

        using (var scope = Factory.Services.CreateScope())
        {
            var liveStore = scope.ServiceProvider.GetRequiredService<LiveLocationRedisStore>();
            await liveStore.RemoveLiveLocationAsync(group.Id, owner.Id);
        }

        // Act
        var res = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/mine?groupId={group.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);

        var secondRes = await Client.GetAsync(
            $"{LocationIntegrationTestHelpers.SessionsBase}/mine?groupId={group.Id}",
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(secondRes, HttpStatusCode.NoContent);
    }
}
