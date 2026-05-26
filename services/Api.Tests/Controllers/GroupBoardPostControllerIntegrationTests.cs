using System.Net;
using System.Net.Http.Json;
using Api.Domain.Comments.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Posts.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupBoardPostControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task CreateAndGetPost_Returns401_WhenStrangerOnPrivateMembersOnlyBoard()
    {
        const string testMethodName = "GbpPrivateStranger";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var boardRes = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "General" });
        Assert.Equal(HttpStatusCode.Created, boardRes.StatusCode);
        var board = (await boardRes.Content.ReadFromJsonAsync<GroupBoardResponseDto>())!;

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);
        var create = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts",
            new GroupBoardPostCreateRequestDto { Title = "t", Content = "c" });
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task CreatePostAndComment_Succeeds_ForMemberOnPrivateGroup()
    {
        const string testMethodName = "GbpMemberOk";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var boardRes = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards",
            new GroupBoardCreateRequestDto { Name = "General" });
        var board = (await boardRes.Content.ReadFromJsonAsync<GroupBoardResponseDto>())!;

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);
        var createPost = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts",
            new GroupBoardPostCreateRequestDto { Title = "Hello", Content = "World" });
        Assert.Equal(HttpStatusCode.Created, createPost.StatusCode);

        var list = await Client.GetAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/boards/{board.Id}/posts");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var posts = await list.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(posts);
        Assert.Single(posts);

        var comment = await Client.PostAsJsonAsync("/api/comments", new CommentCreateRequestDto
        {
            PostId = posts[0].Id,
            Content = "nice",
        });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
    }
}
