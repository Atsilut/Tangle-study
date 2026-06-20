using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Posts.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class MapPinControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private const decimal SeoulLat = 37.5665m;
    private const decimal SeoulLng = 126.9780m;

    [Fact]
    public async Task CreateMapPin_Returns201_WithBody()
    {
        // Arrange
        const string testMethodName = "MapPinCreate";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var req = new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng };

        // Act
        var res = await Client.PostAsJsonAsync("/api/location/pins", req, TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;
        var req = new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng };

        // Act
        var res = await Client.PostAsJsonAsync("/api/location/pins", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMapPin_Returns201_WhenLinkedToOwnPost()
    {
        // Arrange
        const string testMethodName = "MapPinCreateForPost";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var postRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Map post", Content = "content" },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var posts = await Client.GetFromJsonAsync<List<PostGetResponseDto>>("/api/posts", TestContext.Current.CancellationToken);
        var post = posts!.Single();

        // Act
        var res = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng, PostId = post.Id },
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var dto = await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(dto);
        Assert.Equal(post.Id, dto.PostId);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns200_WithPins()
    {
        // Arrange
        const string testMethodName = "MapPinBounds";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        // Act — authenticated viewer sees own standalone pin
        var res = await Client.GetAsync(
            $"/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<MapPinGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(SeoulLat, list[0].Latitude);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns401_WhenUnauthenticated()
    {
        // Act
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns204_WhenNoPins()
    {
        // Arrange
        const string testMethodName = "MapPinBoundsEmpty";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);

        // Act
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=0&maxLatitude=1&minLongitude=0&maxLongitude=1",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinById_Returns200_WhenVisible()
    {
        // Arrange
        const string testMethodName = "MapPinGetById";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        // Act
        var res = await Client.GetAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
    }

    [Fact]
    public async Task GetMapPinById_Returns404_WhenMissing()
    {
        // Arrange
        const string testMethodName = "MapPinGetMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);

        // Act
        var res = await Client.GetAsync("/api/location/pins/99999", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMapPin_Returns204_ForOwner()
    {
        // Arrange
        const string testMethodName = "MapPinDelete";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        // Act
        var res = await Client.DeleteAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        var getRes = await Client.GetAsync($"/api/location/pins/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMapPin_Returns401_WhenNonOwner()
    {
        // Arrange
        const string testMethodName = "MapPinDeleteNonOwner";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 1);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        var createRes = await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken);

        var other = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, other);

        // Act
        var res = await Client.DeleteAsync($"/api/location/pins/{created!.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMapPinsInBounds_HidesPinsFromBlockedUser()
    {
        // Arrange
        const string testMethodName = "MapPinBlockFilter";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 1);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);

        var viewer = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, viewer);
        await Client.PostAsJsonAsync(
            "/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = owner.Id },
            TestContext.Current.CancellationToken);

        // Act
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinsInBounds_Returns204_Not401_WhenPrivateGroupPostPinInBounds()
    {
        // Arrange — private group post with location; stranger must not trigger 401 on the whole bbox query
        const string testMethodName = "MapPinPrivateGroupFilter";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 1);
        var member = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 2);
        var stranger = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 3);

        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(
            Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);
        var postRes = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts",
            new GroupBoardPostCreateRequestDto
            {
                Title = "Secret place",
                Content = "Only members see this",
                Latitude = SeoulLat,
                Longitude = SeoulLng,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        // Act
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMapPinsInBounds_OmitsPrivateGroupPostPin_ButReturnsVisiblePins()
    {
        // Arrange — public standalone pin plus hidden private group post pin in the same bbox
        const string testMethodName = "MapPinMixedVisibility";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 1);
        var member = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 2);
        var stranger = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName, index: 3);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        await Client.PostAsJsonAsync(
            "/api/location/pins",
            new MapPinCreateRequestDto { Latitude = SeoulLat, Longitude = SeoulLng },
            TestContext.Current.CancellationToken);

        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(
            Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);
        await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts",
            new GroupBoardPostCreateRequestDto
            {
                Title = "Secret place",
                Content = "Only members see this",
                Latitude = 37.57m,
                Longitude = 126.98m,
            },
            TestContext.Current.CancellationToken);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        // Act
        var res = await Client.GetAsync(
            "/api/location/pins?minLatitude=37&maxLatitude=38&minLongitude=126&maxLongitude=127",
            TestContext.Current.CancellationToken);

        // Assert — stranger sees neither the private post pin nor the owner's standalone pin
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }
}
