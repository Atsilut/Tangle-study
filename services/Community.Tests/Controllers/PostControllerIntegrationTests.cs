using System.Net;
using System.Net.Http.Json;
using Community.Dto;
using Community.Tests.Infrastructure;

namespace Community.Tests.Controllers;

[Collection(CommunityIntegrationTestCollection.Name)]
public sealed class PostControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task CreateAndGetPost_ReturnsCreatedPost()
    {
        var userId = InMemoryUser.SeedUser("alice");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Hello", Content = "World" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
            TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!);
        Assert.Equal("Hello", post.Title);
        Assert.Equal("World", post.Content);
        Assert.Equal(userId, post.AuthorId);
        Assert.Equal("alice", post.AuthorNickname);

        var getRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal(post.Id, fetched!.Id);
        Assert.Equal("Hello", fetched.Title);
        Assert.Equal("World", fetched.Content);
        Assert.Equal(userId, fetched.AuthorId);
        Assert.Equal("alice", fetched.AuthorNickname);
    }

    [Fact]
    public async Task UpdateAndDeletePost_AsAuthor_Succeeds()
    {
        var userId = InMemoryUser.SeedUser("bob");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Old", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var post = Assert.Single(
            (await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
                TestContext.Current.CancellationToken))!);

        var patchRes = await Client.PatchAsJsonAsync(
            "/api/posts",
            new PostPatchRequestDto { Id = post.Id, Title = "New", Content = "Updated" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var getRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var updated = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal("New", updated!.Title);
        Assert.Equal("Updated", updated.Content);

        var deleteRes = await Client.DeleteAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var missingRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingRes.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_ReturnsAuthorPosts()
    {
        var userId = InMemoryUser.SeedUser("carol");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Nick", Content = "Post" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var res = await Client.GetAsync("/api/posts/nickname/carol", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var posts = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
            TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!);
        Assert.Equal("Nick", post.Title);
        Assert.Equal("Post", post.Content);
        Assert.Equal(userId, post.AuthorId);
        Assert.Equal("carol", post.AuthorNickname);
    }

    [Fact]
    public async Task CreatePost_WithMediaAndLocation_ReturnsThemOnGet()
    {
        var userId = InMemoryUser.SeedUser("media-user");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var assetId = FakeMedia.SeedReadyAsset();

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "With media",
                Content = "And location",
                MediaAssetIds = [assetId],
                Latitude = 37.5m,
                Longitude = 127.0m,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!);
        var media = Assert.Single(post.Media);
        Assert.Equal(assetId, media.Id);
        Assert.NotNull(post.Location);
        Assert.Equal(37.5m, post.Location.Latitude);
        Assert.Equal(127.0m, post.Location.Longitude);
    }

    [Fact]
    public async Task UpdatePost_PatchesMediaAndLocation_ThenClearsLocation()
    {
        var userId = InMemoryUser.SeedUser("patch-media");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var firstAsset = FakeMedia.SeedReadyAsset(fileName: "a.png");
        var secondAsset = FakeMedia.SeedReadyAsset(fileName: "b.png");

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Media",
                Content = "Body",
                MediaAssetIds = [firstAsset],
                Latitude = 1m,
                Longitude = 2m,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        var patchRes = await Client.PatchAsJsonAsync(
            "/api/posts",
            new PostPatchRequestDto
            {
                Id = postId,
                Title = "Media",
                Content = "Body",
                AddMediaAssetIds = [secondAsset],
                RemoveMediaAssetIds = [firstAsset],
                Latitude = 10m,
                Longitude = 20m,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var getRes = await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        var post = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        var media = Assert.Single(post!.Media);
        Assert.Equal(secondAsset, media.Id);
        Assert.Equal(10m, post.Location!.Latitude);
        Assert.Equal(20m, post.Location.Longitude);

        var clearRes = await Client.PatchAsJsonAsync(
            "/api/posts",
            new PostPatchRequestDto
            {
                Id = postId,
                Title = "Media",
                Content = "Body",
                ClearLocation = true,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, clearRes.StatusCode);

        var cleared = await (await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<PostGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.Null(cleared!.Location);
    }

    [Fact]
    public async Task DeletePost_RemovesLinkedMediaAndLocation()
    {
        var userId = InMemoryUser.SeedUser("delete-media");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var assetId = FakeMedia.SeedReadyAsset();

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Temp",
                Content = "Body",
                MediaAssetIds = [assetId],
                Latitude = 5m,
                Longitude = 6m,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        var deleteRes = await Client.DeleteAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var media = await FakeMedia.GetMediaForPostAsync(postId);
        Assert.Empty(media);
        var locations = await FakeLocation.GetLocationsByPostIdsAsync(
            [postId],
            TestContext.Current.CancellationToken);
        Assert.False(locations.ContainsKey(postId));
    }

    [Fact]
    public async Task CreatePost_Unauthenticated_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "No", Content = "Auth" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeletePost_AsNonAuthor_ReturnsForbidden()
    {
        var authorId = InMemoryUser.SeedUser("author");
        var otherId = InMemoryUser.SeedUser("other");
        GatewayTestAuthHelpers.LoginAs(Client, authorId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Owned", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        GatewayTestAuthHelpers.LoginAs(Client, otherId);
        var patchRes = await Client.PatchAsJsonAsync(
            "/api/posts",
            new PostPatchRequestDto { Id = postId, Title = "Hijack", Content = "Nope" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, patchRes.StatusCode);

        var deleteRes = await Client.DeleteAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task CreatePost_PartialGroupIds_ReturnsBadRequest()
    {
        var userId = InMemoryUser.SeedUser("partial-group");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var res = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Bad", Content = "Group", GroupId = 1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreatePost_LatitudeWithoutLongitude_ReturnsBadRequest()
    {
        var userId = InMemoryUser.SeedUser("bad-loc");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var res = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Bad", Content = "Loc", Latitude = 1m },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetAllPosts_FiltersBlockedAuthors()
    {
        var authorId = InMemoryUser.SeedUser("blocked-author");
        var viewerId = InMemoryUser.SeedUser("viewer");
        InMemoryUser.AddMutualBlock(authorId, viewerId);

        GatewayTestAuthHelpers.LoginAs(Client, authorId);
        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Hidden", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        GatewayTestAuthHelpers.LoginAs(Client, viewerId);
        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, listRes.StatusCode);

        var getRes = await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        var nickRes = await Client.GetAsync("/api/posts/nickname/blocked-author", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, nickRes.StatusCode);
    }

    [Fact]
    public async Task GroupBoardPosts_CreateListAndGet_SucceedsWhenWritable()
    {
        const long groupId = 10;
        const long boardId = 20;
        var userId = InMemoryUser.SeedUser("board-writer");
        GroupAccess.AllowBoard(groupId, boardId);
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            new GroupBoardPostCreateRequestDto { Title = "Board", Content = "Post" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
            TestContext.Current.CancellationToken);
        var post = Assert.Single(posts!);
        Assert.Equal("Board", post.Title);
        Assert.Equal("Post", post.Content);
        Assert.Equal(userId, post.AuthorId);

        var getRes = await Client.GetAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts/{post.Id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal(post.Id, fetched!.Id);
    }

    [Fact]
    public async Task GroupBoardPosts_WriteDenied_ReturnsForbidden()
    {
        const long groupId = 11;
        const long boardId = 21;
        var userId = InMemoryUser.SeedUser("no-write");
        GroupAccess.AllowAllBoards = false;
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            new GroupBoardPostCreateRequestDto { Title = "Denied", Content = "Post" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);
    }

    [Fact]
    public async Task GroupBoardPosts_ViewDenied_ReturnsForbidden()
    {
        const long groupId = 12;
        const long boardId = 22;
        var writerId = InMemoryUser.SeedUser("writer");
        GroupAccess.AllowBoard(groupId, boardId);
        GatewayTestAuthHelpers.LoginAs(Client, writerId);

        var createRes = await Client.PostAsJsonAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            new GroupBoardPostCreateRequestDto { Title = "Secret", Content = "Post" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        GroupAccess.ViewableBoards.Clear();
        GroupAccess.WritableBoards.Clear();
        GroupAccess.AllowAllBoards = false;

        var listRes = await Client.GetAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);
    }

    [Fact]
    public async Task GetPostById_GroupPost_ViewDenied_ReturnsForbidden()
    {
        const long groupId = 13;
        const long boardId = 23;
        var writerId = InMemoryUser.SeedUser("group-author");
        GroupAccess.AllowBoard(groupId, boardId);
        GatewayTestAuthHelpers.LoginAs(Client, writerId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Group",
                Content = "Via public route",
                GroupId = groupId,
                GroupBoardId = boardId,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync(
            $"/api/groups/{groupId}/boards/{boardId}/posts",
            TestContext.Current.CancellationToken);
        var postId = Assert.Single(
            (await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
                TestContext.Current.CancellationToken))!).Id;

        GroupAccess.ViewableBoards.Clear();
        GroupAccess.WritableBoards.Clear();
        GroupAccess.AllowAllBoards = false;

        var getRes = await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);
    }

    [Fact]
    public async Task GetAllPosts_OrdersByCreatedAtDescending()
    {
        var userId = InMemoryUser.SeedUser("order-user");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "First", Content = "Body" },
            TestContext.Current.CancellationToken);
        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Second", Content = "Body" },
            TestContext.Current.CancellationToken);

        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.Equal(2, posts!.Count);
        Assert.Equal("Second", posts[0].Title);
        Assert.Equal("First", posts[1].Title);
    }
}
