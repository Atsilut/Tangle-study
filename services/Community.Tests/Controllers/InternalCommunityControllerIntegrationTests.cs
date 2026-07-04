using System.Net;
using System.Net.Http.Json;
using Community.Api;
using Community.Dto;
using Community.Tests.Infrastructure;

namespace Community.Tests.Controllers;

[Collection(CommunityIntegrationTestCollection.Name)]
public sealed class InternalCommunityControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task DetachOnDeletion_DetachesAuthorFromPostsAndComments()
    {
        var userId = MonolithAccess.SeedUser("frank");
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
            new CommentCreateRequestDto { PostId = postId, Content = "Comment" },
            TestContext.Current.CancellationToken);

        CommunityTestAuthHelpers.LoginAsInternal(Client);
        var detachRes = await Client.PostAsync(
            $"/internal/community/users/{userId}/detach-on-deletion",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, detachRes.StatusCode);

        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        var getRes = await Client.GetAsync($"/api/posts/{postId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var post = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Equal(userId, post!.AuthorId);
    }

    [Fact]
    public async Task DeleteAllByGroup_RemovesGroupPosts()
    {
        var userId = MonolithAccess.SeedUser("grace");
        CommunityTestAuthHelpers.LoginAs(Client, userId);

        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto
            {
                Title = "Group post",
                Content = "Body",
                GroupId = 10,
                GroupBoardId = 20,
            },
            TestContext.Current.CancellationToken);

        CommunityTestAuthHelpers.LoginAsInternal(Client);
        var deleteRes = await Client.PostAsync(
            "/internal/community/groups/10/delete-all",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        Client.DefaultRequestHeaders.Authorization = null;
        Client.DefaultRequestHeaders.Remove("X-Internal-Secret");
        CommunityTestAuthHelpers.LoginAs(Client, userId);
        var listRes = await Client.GetAsync(
            "/api/groups/10/boards/20/posts",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, listRes.StatusCode);
    }

    [Fact]
    public async Task ViewableIds_ReturnsPublicPosts()
    {
        var userId = MonolithAccess.SeedUser("hank");
        CommunityTestAuthHelpers.LoginAs(Client, userId);
        await Client.PostAsJsonAsync(
            "/api/posts",
            new PostCreateRequestDto { Title = "Visible", Content = "Body" },
            TestContext.Current.CancellationToken);
        var posts = await (await Client.GetAsync("/api/posts", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken);
        var postId = Assert.Single(posts!).Id;

        CommunityTestAuthHelpers.LoginAsInternal(Client, userId);
        var res = await Client.PostAsJsonAsync(
            "/internal/community/viewable-ids",
            new InternalCommunityViewableIdsRequestDto([postId], userId),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<InternalCommunityViewableIdsResponseDto>(
            TestContext.Current.CancellationToken);
        Assert.Contains(postId, payload!.ViewablePostIds);
    }
}
