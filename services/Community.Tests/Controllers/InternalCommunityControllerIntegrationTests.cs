using System.Net;
using System.Net.Http.Json;
using Community.Api;
using Community.Dto;
using Community.Tests.Infrastructure;
using Tangle.TestSupport.Auth;

namespace Community.Tests.Controllers;

[Collection(CommunityIntegrationTestCollection.Name)]
public sealed class InternalCommunityControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task DetachOnDeletion_DetachesAuthorFromPostsAndComments()
    {
        var userId = InMemoryUser.SeedUser("frank");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createPostRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createPostRes.StatusCode);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var postId = Assert.Single(posts!).Id;

        var createCommentRes = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Comment" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createCommentRes.StatusCode);
        var comments = await (await Client.GetAsync(
                $"/api/comments/post/{postId}",
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken);
        var commentId = Assert.Single(comments!).Id;

        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var detachRes = await Client.PostAsync(
            $"/internal/community/users/{userId}/detach-on-deletion",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, detachRes.StatusCode);

        var postEntity = await FindPostEntityAsync(postId);
        Assert.NotNull(postEntity);
        Assert.Null(postEntity.UserId);
        Assert.Equal(userId, postEntity.DeletedUserId);

        var commentEntity = await FindCommentEntityAsync(commentId);
        Assert.NotNull(commentEntity);
        Assert.Null(commentEntity.UserId);
        Assert.Equal(userId, commentEntity.DeletedUserId);

        InMemoryUser.SimulateUserDeleted(userId);

        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        var getRes = await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var post = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal(userId, post!.AuthorId);
        Assert.Equal("Deleted User", post.AuthorNickname);

        var commentListRes = await Client.GetAsync(
            $"/api/comments/post/{postId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, commentListRes.StatusCode);
        var detachedComments = await commentListRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(
            TestContext.Current.CancellationToken);
        var comment = Assert.Single(detachedComments!);
        Assert.Equal(userId, comment.AuthorId);
        Assert.Null(comment.UserId);
        Assert.Equal(userId, comment.DeletedUserId);
        Assert.Equal("Deleted User", comment.AuthorNickname);
    }

    [Fact]
    public async Task DeleteAllByGroup_RemovesGroupPostsAndLocations()
    {
        var userId = InMemoryUser.SeedUser("grace");
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Group post",
                Content = "Body",
                GroupId = 10,
                GroupBoardId = 20,
                Latitude = 12m,
                Longitude = 34m,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var beforeRes = await Client.GetAsync(
            "/api/groups/10/boards/20/posts",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, beforeRes.StatusCode);
        var postId = Assert.Single(
            (await beforeRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
                TestContext.Current.CancellationToken))!).Id;
        Assert.True(
            (await FakeLocation.GetLocationsByPostIdsAsync([postId], TestContext.Current.CancellationToken))
                .ContainsKey(postId));

        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var deleteRes = await Client.PostAsync(
            "/internal/community/groups/10/delete-all",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        Assert.False(
            (await FakeLocation.GetLocationsByPostIdsAsync([postId], TestContext.Current.CancellationToken))
                .ContainsKey(postId));

        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var listRes = await Client.GetAsync(
            "/api/groups/10/boards/20/posts",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, listRes.StatusCode);
    }

    [Fact]
    public async Task ViewableIds_ReturnsPublicPosts()
    {
        var userId = InMemoryUser.SeedUser("hank");
        GatewayTestAuthHelpers.LoginAs(Client, userId);
        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Visible", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var postId = Assert.Single(posts!).Id;

        GatewayTestAuthHelpers.LoginAsInternal(Client, userId);
        var res = await Client.PostAsJsonAsync(
            "/internal/community/viewable-ids",
            new InternalCommunityViewableIdsRequestDto([postId], userId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<InternalCommunityViewableIdsResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Contains(postId, payload!.ViewablePostIds);
    }

    [Fact]
    public async Task ViewableIds_ExcludesBlockedAndInaccessibleGroupPosts()
    {
        var authorId = InMemoryUser.SeedUser("author");
        var viewerId = InMemoryUser.SeedUser("viewer");
        GroupAccess.AllowBoard(groupId: 30, boardId: 40);

        GatewayTestAuthHelpers.LoginAs(Client, authorId);
        var publicCreate = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Public", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, publicCreate.StatusCode);
        var publicPostId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        var groupCreate = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Group",
                Content = "Body",
                GroupId = 30,
                GroupBoardId = 40,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, groupCreate.StatusCode);
        var groupPostId = Assert.Single(
            (await (await Client.GetAsync("/api/groups/30/boards/40/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        InMemoryUser.AddMutualBlock(authorId, viewerId);
        GroupAccess.ViewableBoards.Clear();
        GroupAccess.WritableBoards.Clear();
        GroupAccess.AllowAllBoards = false;

        GatewayTestAuthHelpers.LoginAsInternal(Client, viewerId);
        var res = await Client.PostAsJsonAsync(
            "/internal/community/viewable-ids",
            new InternalCommunityViewableIdsRequestDto([publicPostId, groupPostId], viewerId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<InternalCommunityViewableIdsResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Empty(payload!.ViewablePostIds);
    }

    [Fact]
    public async Task ValidatePostOwner_AsOwner_Succeeds_AsOther_ReturnsForbidden()
    {
        var ownerId = InMemoryUser.SeedUser("owner");
        var otherId = InMemoryUser.SeedUser("not-owner");
        GatewayTestAuthHelpers.LoginAs(Client, ownerId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Owned", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        GatewayTestAuthHelpers.LoginAsInternal(Client, ownerId);
        var ownerRes = await Client.PostAsync(
            $"/internal/community/{postId}/validate-owner",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, ownerRes.StatusCode);

        GatewayTestAuthHelpers.LoginAsInternal(Client, otherId);
        var otherRes = await Client.PostAsync(
            $"/internal/community/{postId}/validate-owner",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, otherRes.StatusCode);
    }

    [Fact]
    public async Task MediaView_GroupPost_DeniedWhenBoardNotViewable()
    {
        const long groupId = 50;
        const long boardId = 60;
        var userId = InMemoryUser.SeedUser("media-view");
        GroupAccess.AllowBoard(groupId, boardId);
        GatewayTestAuthHelpers.LoginAs(Client, userId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Group media",
                Content = "Body",
                GroupId = groupId,
                GroupBoardId = boardId,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync(
                    $"/api/groups/{groupId}/boards/{boardId}/posts",
                    TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        GroupAccess.ViewableBoards.Clear();
        GroupAccess.WritableBoards.Clear();
        GroupAccess.AllowAllBoards = false;

        GatewayTestAuthHelpers.LoginAsInternal(Client, userId);
        var denied = await Client.PostAsync(
            $"/internal/community/{postId}/media-view",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        GroupAccess.AllowBoard(groupId, boardId, writable: false);
        var allowed = await Client.PostAsync(
            $"/internal/community/{postId}/media-view",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task MediaView_MissingPost_ReturnsNotFound()
    {
        GatewayTestAuthHelpers.LoginAsInternal(Client, userId: 1);
        var res = await Client.PostAsync(
            "/internal/community/999999/media-view",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MediaView_BlockedAuthor_ReturnsNotFound()
    {
        var authorId = InMemoryUser.SeedUser("media-author");
        var viewerId = InMemoryUser.SeedUser("media-viewer");
        GatewayTestAuthHelpers.LoginAs(Client, authorId);

        var createRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Blocked media", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        InMemoryUser.AddMutualBlock(authorId, viewerId);
        GatewayTestAuthHelpers.LoginAsInternal(Client, viewerId);
        var res = await Client.PostAsync(
            $"/internal/community/{postId}/media-view",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
