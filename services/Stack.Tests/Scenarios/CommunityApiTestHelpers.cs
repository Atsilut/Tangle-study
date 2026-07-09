using System.Net;
using System.Net.Http.Json;
using Community.Dto;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;

namespace Stack.Tests.Scenarios;

public static class CommunityApiTestHelpers
{
    public const string PostsBase = "/api/posts";
    private const string CommentsBase = "/api/comments";

    public static async Task<PostGetResponseDto> CreatePostAsync(
        HttpClient client,
        long authorUserId,
        string title,
        string content,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, authorUserId, TestContext.Current.CancellationToken);
        var res = await client.PostAsJsonAsync(
            PostsBase,
            new PostCreateRequestDto { Title = title, Content = content },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var posts = await ListPostsAsync(client, authorUserId, auth);
        return posts.Single(p => p.Title == title && p.Content == content);
    }

    public static async Task<CommentGetResponseDto> CreateCommentAsync(
        HttpClient client,
        long authorUserId,
        long postId,
        string content,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, authorUserId, TestContext.Current.CancellationToken);
        var res = await client.PostAsJsonAsync(
            CommentsBase,
            new CommentCreateRequestDto { PostId = postId, Content = content },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var comments = await ListCommentsAsync(client, authorUserId, postId, auth);
        return comments.Single(c => c.Content == content && c.AuthorId == authorUserId);
    }

    public static async Task<List<PostGetResponseDto>> ListPostsAsync(
        HttpClient client,
        long asUserId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, asUserId, TestContext.Current.CancellationToken);
        var res = await client.GetAsync(PostsBase, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<List<CommentGetResponseDto>> ListCommentsAsync(
        HttpClient client,
        long asUserId,
        long postId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, asUserId, TestContext.Current.CancellationToken);
        var res = await client.GetAsync($"{CommentsBase}/post/{postId}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }
}
