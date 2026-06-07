using System.Net;
using System.Net.Http.Json;
using Api.Domain.Comments.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Posts.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupBoardCommentAccessIntegrationTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    [Fact]
    public async Task GroupBoardComment_UpdateAndDelete_Return401_AfterMemberRemoved()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("gb_comment_mut");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(
            Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        const string commentContent = "member comment before removal";
        var (_, comment) = await CreateBoardPostAndCommentAsync(
            scenario, group.Id, board.Id, GroupActorRole.Member, commentContent);

        await scenario.LoginAsAsync(GroupActorRole.Owner);
        var removeRes = await Client.DeleteAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{scenario.Member.Id}");
        await IntegrationAssertions.AssertStatusAsync(removeRes, HttpStatusCode.NoContent);
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
        await scenario.LoginAsAsync(GroupActorRole.Member);

        // Act
        var updateRes = await Client.PatchAsJsonAsync(
            "/api/comments",
            new CommentPatchRequestDto { Id = comment.Id, Content = "updated after removal" });
        var deleteRes = await Client.DeleteAsync($"/api/comments/{comment.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(updateRes, HttpStatusCode.Unauthorized);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.Unauthorized);

        await scenario.LoginAsAsync(GroupActorRole.Owner);
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();
        Assert.Equal(commentContent, dto!.Content);
    }

    [Fact]
    public async Task GroupBoardComment_ReadAndCreate_Return401_AfterMemberRemoved()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("gb_comment_read");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        var board = await GroupIntegrationTestHelpers.SeedBoardAsync(
            Factory, group.Id, "MembersOnly", BoardVisibility.MembersOnly);

        var (postId, comment) = await CreateBoardPostAndCommentAsync(
            scenario, group.Id, board.Id, GroupActorRole.Member, "existing comment");

        await scenario.LoginAsAsync(GroupActorRole.Owner);
        var removeRes = await Client.DeleteAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{scenario.Member.Id}");
        await IntegrationAssertions.AssertStatusAsync(removeRes, HttpStatusCode.NoContent);
        await scenario.LoginAsAsync(GroupActorRole.Member);

        // Act
        var getByIdRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        var getByPostRes = await Client.GetAsync($"/api/comments/post/{postId}");
        var createRes = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = postId, Content = "new after removal" });

        // Assert
        await IntegrationAssertions.AssertStatusAsync(getByIdRes, HttpStatusCode.Unauthorized);
        await IntegrationAssertions.AssertStatusAsync(getByPostRes, HttpStatusCode.Unauthorized);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Unauthorized);
    }

    private async Task<(long PostId, CommentGetResponseDto Comment)> CreateBoardPostAndCommentAsync(
        GroupIntegrationScenario scenario,
        long groupId,
        long boardId,
        GroupActorRole author,
        string commentContent)
    {
        await scenario.LoginAsAsync(author);
        var postsBase = $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/boards/{boardId}/posts";
        var title = $"post_{Guid.NewGuid():N}";
        var createPostRes = await Client.PostAsJsonAsync(
            postsBase,
            new GroupBoardPostCreateRequestDto { Title = title, Content = "body" });
        await IntegrationAssertions.AssertStatusAsync(createPostRes, HttpStatusCode.Created);

        var listRes = await Client.GetAsync(postsBase);
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var posts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var post = posts!.Single(p => p.Title == title);

        var createCommentRes = await Client.PostAsJsonAsync(
            "/api/comments",
            new CommentCreateRequestDto { PostId = post.Id, Content = commentContent });
        await IntegrationAssertions.AssertStatusAsync(createCommentRes, HttpStatusCode.Created);

        var getCommentsRes = await Client.GetAsync($"/api/comments/post/{post.Id}");
        await IntegrationAssertions.AssertStatusAsync(getCommentsRes, HttpStatusCode.OK);
        var commentTree = await getCommentsRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var comment = FindCommentByContent(commentTree!, commentContent);
        Assert.NotNull(comment);

        return (post.Id, comment);
    }

    private static CommentGetResponseDto? FindCommentByContent(
        IEnumerable<CommentGetResponseDto> comments,
        string content)
    {
        foreach (var comment in comments)
        {
            if (comment.Content == content) return comment;
            var inReplies = FindCommentByContent(comment.Replies, content);
            if (inReplies is not null) return inReplies;
        }
        return null;
    }
}
