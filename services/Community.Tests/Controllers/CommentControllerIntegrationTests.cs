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

        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
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

        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Post", Content = "Body" },
            TestContext.Current.CancellationToken);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var postId = Assert.Single(posts!).Id;

        await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "To delete" },
            TestContext.Current.CancellationToken);
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
}
