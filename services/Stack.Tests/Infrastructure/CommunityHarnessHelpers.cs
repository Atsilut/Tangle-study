using Community.Dto;
using Tangle.TestSupport.Scenarios;
using Tangle.TestSupport.Auth;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

internal static class CommunityHarnessHelpers
{
    private static ITestAuth Auth => HarnessJwtAuth.Instance;

    public static Task<PostGetResponseDto> CreatePostAsync(
        HttpClient client,
        UserGetResponseDto author,
        string title,
        string content) =>
        CommunityApiTestHelpers.CreatePostAsync(client, author.Id, title, content, Auth);

    public static Task<List<PostGetResponseDto>> ListPostsAsync(HttpClient client, UserGetResponseDto asUser) =>
        CommunityApiTestHelpers.ListPostsAsync(client, asUser.Id, Auth);

    public static Task<CommentGetResponseDto> CreateCommentAsync(
        HttpClient client,
        UserGetResponseDto author,
        long postId,
        string content) =>
        CommunityApiTestHelpers.CreateCommentAsync(client, author.Id, postId, content, Auth);

    public static Task<List<CommentGetResponseDto>> ListCommentsByPostAsync(
        HttpClient client,
        UserGetResponseDto asUser,
        long postId) =>
        CommunityApiTestHelpers.ListCommentsAsync(client, asUser.Id, postId, Auth);
}
