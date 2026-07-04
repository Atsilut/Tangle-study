using System.Net;
using System.Net.Http.Json;
using Community.Dto;
using Community.Tests.Infrastructure;

namespace Community.Tests.Controllers;

[Collection(CommunityIntegrationTestCollection.Name)]
public sealed class CommentControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task CreateAndListComments_ByPostId()
    {
        var userId = MonolithAccess.SeedUser("dave");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

        var createPostRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createPostRes.StatusCode);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var postId = Assert.Single(posts!).Id;

        var createRes = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Nice post" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync($"/api/comments/post/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var comments = await listRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(
            TestContext.Current.CancellationToken);
        var comment = Assert.Single(comments!);
        Assert.Equal("Nice post", comment.Content);
        Assert.Equal(postId, comment.PostId);
        Assert.Equal(userId, comment.AuthorId);
    }

    [Fact]
    public async Task DeleteComment_AsAuthor_Succeeds()
    {
        var userId = MonolithAccess.SeedUser("erin");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

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
            new CommentCreateRequestDto { PostId = postId, Content = "To delete" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createCommentRes.StatusCode);
        var comments = await (await Client.GetAsync(
                $"/api/comments/post/{postId}",
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken);
        var commentId = Assert.Single(comments!).Id;

        var deleteRes = await Client.DeleteAsync(
            $"/api/comments/{commentId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var emptyRes = await Client.GetAsync($"/api/comments/post/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, emptyRes.StatusCode);
    }

    [Fact]
    public async Task CommentsOnBlockedAuthorPost_AreHiddenAndCreateDenied()
    {
        var authorId = MonolithAccess.SeedUser("post-author");
        var commenterId = MonolithAccess.SeedUser("commenter");
        var viewerId = MonolithAccess.SeedUser("blocked-viewer");

        CommunityTestAuthHelpers.LoginAs(Client, authorId);
        var createPostRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createPostRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        CommunityTestAuthHelpers.LoginAs(Client, commenterId);
        var createCommentRes = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Visible to friends" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createCommentRes.StatusCode);

        MonolithAccess.AddMutualBlock(authorId, viewerId);
        CommunityTestAuthHelpers.LoginAs(Client, viewerId);

        var listRes = await Client.GetAsync($"/api/comments/post/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, listRes.StatusCode);

        var createDenied = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Should fail" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, createDenied.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByPostId_OrdersRootsAndRepliesChronologically()
    {
        var userId = MonolithAccess.SeedUser("order-commenter");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

        var createPostRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createPostRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Root first" },
            TestContext.Current.CancellationToken);
        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Root second" },
            TestContext.Current.CancellationToken);

        var roots = await (await Client.GetAsync(
                $"/api/comments/post/{postId}",
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.Equal(2, roots!.Count);
        Assert.Equal("Root first", roots[0].Content);
        Assert.Equal("Root second", roots[1].Content);

        var parentId = roots[0].Id;
        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, ParentId = parentId, Content = "Reply first" },
            TestContext.Current.CancellationToken);
        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, ParentId = parentId, Content = "Reply second" },
            TestContext.Current.CancellationToken);

        var withReplies = await (await Client.GetAsync(
                $"/api/comments/post/{postId}",
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.Equal(["Reply first", "Reply second"], withReplies![0].Replies.Select(r => r.Content));
    }

    [Fact]
    public async Task GetCommentsByUserId_OrdersByCreatedAtDescending()
    {
        var userId = MonolithAccess.SeedUser("user-comments-order");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

        var createPostRes = await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createPostRes.StatusCode);
        var postId = Assert.Single(
            (await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
                .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!).Id;

        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Older" },
            TestContext.Current.CancellationToken);
        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "Newer" },
            TestContext.Current.CancellationToken);

        var comments = await (await Client.GetAsync(
                $"/api/comments/user/{userId}",
                TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.Equal(2, comments!.Count);
        Assert.Equal("Newer", comments[0].Content);
        Assert.Equal("Older", comments[1].Content);
    }
}
