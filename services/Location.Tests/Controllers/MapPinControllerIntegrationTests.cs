using System.Net;
using System.Net.Http.Json;
using Location.Dto;
using Location.Tests.Infrastructure;

namespace Location.Tests.Controllers;

[Collection(LocationIntegrationTestCollection.Name)]
public sealed class MapPinControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : LocationIntegrationTestBase(postgres, redis)
{
    private const decimal SeoulLat = 37.5665m;
    private const decimal SeoulLng = 126.9780m;

    [Fact]
    public async Task CreateMapPin_Returns201_WithBody()
    {
        const string testMethodName = "MapPinCreate";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var req = new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng };

        var res = await Client.PostAsJsonAsync("/api/location/pins", req, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(dto);
        Assert.Equal(SeoulLat, dto.Latitude);
        Assert.Equal(SeoulLng, dto.Longitude);
        Assert.Equal(user.Id, dto.OwnerUserId);
        Assert.Null(dto.PostId);
    }

    [Fact]
    public async Task CreateMapPin_Returns401_WhenUnauthenticated()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        var req = new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng };

        var res = await Client.PostAsJsonAsync("/api/location/pins", req, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMapPin_Returns201_WhenLinkedToOwnPost()
    {
        const string testMethodName = "MapPinCreateForPost";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var postId = MonolithAccess.SeedPost(user.Id);

        var res = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng, PostId = postId },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(dto);
        Assert.Equal(postId, dto.PostId);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns200_WithPins()
    {
        const string testMethodName = "MapPinBounds";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<MapPinGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(SeoulLat, list[0].Latitude);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns401_WhenUnauthenticated()
    {
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns204_WhenNoPins()
    {
        const string testMethodName = "MapPinBoundsEmpty";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);

        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=0&maxLatitude=1&minLongitude=0&maxLongitude=1",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinById_Returns200_WhenVisible()
    {
        const string testMethodName = "MapPinGetById";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        var res = await Client.GetAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
    }

    [Fact]
    public async Task GetMapPinById_Returns404_WhenMissing()
    {
        const string testMethodName = "MapPinGetMissing";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);

        var res = await Client.GetAsync("/api/location/pins/99999", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMapPin_Returns204_ForOwner()
    {
        const string testMethodName = "MapPinDelete";
        var user = CreateUserForTest(testMethodName);
        LoginAs(user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        var res = await Client.DeleteAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        var getRes = await Client.GetAsync($"/api/location/pins/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMapPin_Returns401_WhenNonOwner()
    {
        const string testMethodName = "MapPinDeleteNonOwner";
        var owner = CreateUserForTest(testMethodName, index: 1);
        LoginAs(owner);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        var other = CreateUserForTest(testMethodName, index: 2);
        LoginAs(other);

        var res = await Client.DeleteAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMapPinsInBounds_HidesPinsFromBlockedUser()
    {
        const string testMethodName = "MapPinBlockFilter";
        var owner = CreateUserForTest(testMethodName, index: 1);
        LoginAs(owner);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);

        var viewer = CreateUserForTest(testMethodName, index: 2);
        MonolithAccess.AddBlock(viewer.Id, owner.Id);
        LoginAs(viewer);

        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns204_Not401_WhenPrivateGroupPostPinInBounds()
    {
        const string testMethodName = "MapPinPrivateGroupFilter";
        var owner = CreateUserForTest(testMethodName, index: 1);
        var member = CreateUserForTest(testMethodName, index: 2);
        var stranger = CreateUserForTest(testMethodName, index: 3);

        var groupId = CreateGroupWithOwner(owner);
        AddGroupMember(groupId, member);

        const long postId = 1001;
        MonolithAccess.SeedPost(member.Id, postId);
        MonolithAccess.SetPostViewable(postId, member.Id);

        LoginAs(member);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto
            {
                Latitude = SeoulLat,
                Longitude = SeoulLng,
                PostId = postId,
            },
            TestContext.Current.CancellationToken);

        LoginAs(stranger);

        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinsInBounds_OmitsPrivateGroupPostPin_ButReturnsVisiblePins()
    {
        const string testMethodName = "MapPinMixedVisibility";
        var owner = CreateUserForTest(testMethodName, index: 1);
        var member = CreateUserForTest(testMethodName, index: 2);
        var stranger = CreateUserForTest(testMethodName, index: 3);

        LoginAs(owner);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);

        var groupId = CreateGroupWithOwner(owner);
        AddGroupMember(groupId, member);

        const long postId = 1002;
        MonolithAccess.SeedPost(member.Id, postId);
        MonolithAccess.SetPostViewable(postId, member.Id);

        LoginAs(member);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto
            {
                Latitude = 37.57m,
                Longitude = 126.98m,
                PostId = postId,
            },
            TestContext.Current.CancellationToken);

        LoginAs(stranger);

        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }
}
