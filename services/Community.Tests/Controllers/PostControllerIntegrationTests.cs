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
        var userId = MonolithAccess.SeedUser("alice");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

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
    }

    [Fact]
    public async Task UpdateAndDeletePost_AsAuthor_Succeeds()
    {
        var userId = MonolithAccess.SeedUser("bob");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Old", Content = "Body" },
            TestContext.Current.CancellationToken);
        var listRes = await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken);
        var post = Assert.Single(
            (await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
                TestContext.Current.CancellationToken))!);

        var patchRes = await Client.PatchAsJsonAsync(
            "/api/posts",
            new PostPatchRequestDto { Id = post.Id, Title = "New", Content = "Updated" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var getRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        var updated = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal("New", updated!.Title);

        var deleteRes = await Client.DeleteAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var missingRes = await Client.GetAsync($"/api/posts/{post.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingRes.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_ReturnsAuthorPosts()
    {
        var userId = MonolithAccess.SeedUser("carol");
        CommunityTestAuthHelpers.LoginAs(Client, userId);
        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Nick", Content = "Post" },
            TestContext.Current.CancellationToken);

        var res = await Client.GetAsync("/api/posts/nickname/carol", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var posts = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(
            TestContext.Current.CancellationToken);
        Assert.Single(posts!);
    }
}
