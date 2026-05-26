using Api.Domain.Comments.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class CommentServiceUnitTests
{
    [Fact]
    public async Task UpdateCommentAsync_OnGroupBoardPost_ReturnsUnauthorized_AfterMemberRemoved()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var member = await CreateUserAsync(graph.UserRepository, "member");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        var board = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "board",
            Description = "desc",
        });

        http.HttpContext = ContextFor(member.Id);
        await graph.PostService.CreateGroupBoardPostAsync(
            group.Id,
            board.Id,
            new GroupBoardPostCreateRequestDto { Title = "t", Content = "c" });
        var post = (await graph.PostRepository.GetPostsByGroupBoardAsync(group.Id, board.Id)).Single();
        await graph.CommentService.CreateCommentAsync(new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = "comment",
        });
        var comment = (await graph.CommentRepository.GetCommentsByPostIdAsync(post.Id)).Single();

        http.HttpContext = ContextFor(owner.Id);
        await graph.GroupMembershipService.RemoveMemberInternalAsync(group.Id, member.Id);

        http.HttpContext = ContextFor(member.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.CommentService.UpdateCommentAsync(new CommentPatchRequestDto
            {
                Id = comment.Id,
                Content = "hacked",
            }));
    }

    [Fact]
    public async Task DeleteCommentAsync_OnGroupBoardPost_ReturnsUnauthorized_AfterMemberRemoved()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var member = await CreateUserAsync(graph.UserRepository, "member");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        var board = await graph.GroupBoardService.CreateAsync(group.Id, new GroupBoardCreateRequestDto
        {
            Name = "board",
            Description = "desc",
        });

        http.HttpContext = ContextFor(member.Id);
        await graph.PostService.CreateGroupBoardPostAsync(
            group.Id,
            board.Id,
            new GroupBoardPostCreateRequestDto { Title = "t", Content = "c" });
        var post = (await graph.PostRepository.GetPostsByGroupBoardAsync(group.Id, board.Id)).Single();
        await graph.CommentService.CreateCommentAsync(new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = "comment",
        });
        var comment = (await graph.CommentRepository.GetCommentsByPostIdAsync(post.Id)).Single();

        http.HttpContext = ContextFor(owner.Id);
        await graph.GroupMembershipService.RemoveMemberInternalAsync(group.Id, member.Id);

        http.HttpContext = ContextFor(member.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.CommentService.DeleteCommentAsync(comment.Id));
    }

    [Fact]
    public async Task CreateCommentAsync_ValidRequest_CreatesComment()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = await CreateUserAsync(graph.UserRepository, "test");
        var post = new Post(user.Id, "title", "content");
        await graph.PostRepository.CreatePostAsync(post);
        http.HttpContext = ContextFor(user.Id);

        await graph.CommentService.CreateCommentAsync(new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = "Test comment",
        });

        var comments = await graph.CommentRepository.GetCommentsByPostIdAsync(post.Id);
        Assert.Single(comments);
        Assert.Equal("Test comment", comments[0].Content);
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new User($"{nickname}@test.com", "Password123!", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
