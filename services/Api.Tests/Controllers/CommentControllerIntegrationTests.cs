using Api.Domain.Comments.Dto;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CommentControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private async Task<PostGetResponseDto> CreatePostForTest(string testMethodName, long userId, long index = 1)
    {
        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} Post Title " + index.ToString(),
            Content = $"{testMethodName} Post Content " + index.ToString()
        };
        var create = await Client.PostAsJsonAsync("/api/posts", req);
        await IntegrationAssertions.AssertStatusAsync(create, HttpStatusCode.Created);
        var getAll = await Client.GetAsync("/api/posts");
        var all = await getAll.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        return all!.Single(p => p.Title == req.Title && p.Content == req.Content && p.AuthorId == userId);
    }

    private async Task<CommentGetResponseDto> CreateCommentForTest(
        string testMethodName,
        long postId,
        long index = 1,
        long? parentId = null)
    {
        var req = new CommentCreateRequestDto
        {
            PostId = postId,
            Content = $"{testMethodName} Test " + index.ToString(),
            ParentId = parentId
        };
        var create = await Client.PostAsJsonAsync("/api/comments", req);
        await IntegrationAssertions.AssertStatusAsync(create, HttpStatusCode.Created);
        var getAll = await Client.GetAsync($"/api/comments/post/{postId}");
        var all = await getAll.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var found = FindCommentByContent(all!, req.Content);
        Assert.NotNull(found);
        Assert.Equal(postId, found.PostId);
        if (parentId.HasValue) Assert.Equal(parentId.Value, found.ParentId);
        return found;
    }

    private static CommentGetResponseDto? FindCommentByContent(
        IEnumerable<CommentGetResponseDto> comments,
        string content)
    {
        foreach (var comment in comments)
        {
            if (comment.Content == content) return comment;
            var inReplies = FindCommentByContent(comment.Replies, content);
            if (inReplies != null) return inReplies;
        }
        return null;
    }

    // --- CREATE ---

    [Fact]
    public async Task CreateComment_Returns201()
    {
        // Arrange
        const string testMethodName = "CreateComment";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateComment_Returns401_WhenNotLoggedIn()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;
        const long postId = 1;
        const string content = "Unauthorized Comment";
        var req = new CommentCreateRequestDto
        {
            PostId = postId,
            Content = content
        };
        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateComment_Returns400_WhenPostNotFound()
    {
        // Arrange
        const string testMethodName = "CreateComment_PostNotFound";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        const long nonExistentPostId = 9999; // Assuming this post has been deleted while commenting
        var req = new CommentCreateRequestDto
        {
            PostId = nonExistentPostId,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "Post not found");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task CreateComment_Returns400_WhenContentEmpty(string? invalidContent)
    {
        // Arrange
        const string testMethodName = "CreateComment_ContentEmpty";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);

        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = invalidContent
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateComment_WithParentId_Returns201()
    {
        // Arrange
        const string testMethodName = "CreateNestedComment";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var rootComment = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            ParentId = rootComment.Id,
            Content = $"{testMethodName} Reply"
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var getByPostRes = await Client.GetAsync($"/api/comments/post/{post.Id}");
        var commentTree = await getByPostRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(commentTree);
        var rootDto = commentTree.Single(c => c.Id == rootComment.Id);
        Assert.Single(rootDto.Replies);
        Assert.Equal(req.Content, rootDto.Replies[0].Content);
        Assert.Equal(rootComment.Id, rootDto.Replies[0].ParentId);
    }

    [Fact]
    public async Task CreateComment_Returns400_WhenParentNotFound()
    {
        // Arrange
        const string testMethodName = "CreateNestedComment_ParentMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long missingParentId = 9999;
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            ParentId = missingParentId,
            Content = $"{testMethodName} Orphan Reply"
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.BadRequest, "Parent comment not found");
    }

    [Fact]
    public async Task CreateComment_Returns400_WhenParentOnDifferentPost()
    {
        // Arrange
        const string testMethodName = "CreateNestedComment_ParentWrongPost";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post1 = await CreatePostForTest(testMethodName, user.Id, index: 1);
        var post2 = await CreatePostForTest(testMethodName, user.Id, index: 2);
        var parentOnPost1 = await CreateCommentForTest(testMethodName, post1.Id, index: 1);
        var req = new CommentCreateRequestDto
        {
            PostId = post2.Id,
            ParentId = parentOnPost1.Id,
            Content = $"{testMethodName} Cross-post Reply"
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(
            res, HttpStatusCode.BadRequest, "Parent comment must belong to the same post");
    }

    // --- GET ---

    [Fact]
    public async Task GetCommentsByPost_ReturnsComments()
    {
        // Arrange
        const string testMethodName = "GetCommentsByPost";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment1 = await CreateCommentForTest(testMethodName, post.Id, 1);
        var comment2 = await CreateCommentForTest(testMethodName, post.Id, 2);

        // Act
        var res = await Client.GetAsync($"/api/comments/post/{post.Id}");
        
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var comments = await res.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments.Count);
        Assert.Equal(comment1.Content, comments[0].Content);
        Assert.Equal(comment2.Content, comments[1].Content);
    }

    [Fact]
    public async Task GetCommentsByPost_ReturnsNestedTree()
    {
        // Arrange
        const string testMethodName = "GetCommentsByPost_Nested";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var rootComment = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var replyComment = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: rootComment.Id);
        var nestedReply = await CreateCommentForTest(testMethodName, post.Id, index: 3, parentId: replyComment.Id);
        await CreateCommentForTest(testMethodName, post.Id, index: 4);

        // Act
        var res = await Client.GetAsync($"/api/comments/post/{post.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var commentTree = await res.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(commentTree);
        Assert.Equal(2, commentTree.Count);

        var rootDto = commentTree.Single(c => c.Id == rootComment.Id);
        Assert.Null(rootDto.ParentId);
        Assert.Single(rootDto.Replies);
        Assert.Equal(replyComment.Id, rootDto.Replies[0].Id);
        Assert.Equal(rootComment.Id, rootDto.Replies[0].ParentId);

        Assert.Single(rootDto.Replies[0].Replies);
        Assert.Equal(nestedReply.Id, rootDto.Replies[0].Replies[0].Id);
        Assert.Equal(replyComment.Id, rootDto.Replies[0].Replies[0].ParentId);

        var siblingRoot = commentTree.Single(c => c.Id != rootComment.Id);
        Assert.Null(siblingRoot.ParentId);
        Assert.Empty(siblingRoot.Replies);
    }

    [Fact]
    public async Task GetCommentsByPost_Returns404_WhenPostMissing()
    {
        // Arrange
        const long missingPostId = 9999; // Assuming this post has been deleted while commenting

        // Act
        var res = await Client.GetAsync($"/api/comments/post/{missingPostId}");

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Post not found");
    }

    [Fact]
    public async Task GetCommentsByPost_Returns204_WhenNoComments()
    {
        // Arrange
        const string testMethodName = "GetCommentsByPost_NoComments";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        
        // Act
        var res = await Client.GetAsync($"/api/comments/post/{post.Id}");
        
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCommentById_ReturnsFlatReply_WithParentId()
    {
        // Arrange
        const string testMethodName = "GetCommentById_Nested";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var root = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var reply = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: root.Id);

        // Act
        var res = await Client.GetAsync($"/api/comments/{reply.Id}");
        var dto = await res.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        Assert.NotNull(dto);
        Assert.Equal(reply.Content, dto.Content);
        Assert.Equal(root.Id, dto.ParentId);
        Assert.Empty(dto.Replies);
    }

    [Fact]
    public async Task GetCommentById_Returns200_WhenPostExists()
    {
        // Arrange
        const string testMethodName = "GetCommentById";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        // Act
        var res = await Client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await res.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        Assert.NotNull(dto);
        Assert.Equal(comment.Content, dto.Content);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(post.Id, dto.PostId);
    }

    [Fact]
    public async Task GetCommentById_Returns404_WhenCommentMissing()
    {
        // Arrange
        const long missingCommentId = 9999; // Assuming this comment has been deleted while commenting
        
        // Act
        var res = await Client.GetAsync($"/api/comments/{missingCommentId}");
        
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCommentsByUser_ReturnsComments()
    {
        // Arrange
        const string testMethodName = "GetCommentsByUser";
        var activeUser = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        var lessUser = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Lesser");
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, activeUser);
        var post1 = await CreatePostForTest(testMethodName, activeUser.Id, 1);
        var post2 = await CreatePostForTest(testMethodName, activeUser.Id, 2);
        var comment1 = await CreateCommentForTest(testMethodName, post1.Id, 1);
        var comment2 = await CreateCommentForTest(testMethodName, post2.Id, 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, lessUser);
        var commentLesser = await CreateCommentForTest(testMethodName, post2.Id, 11);

        // Act
        var resActive = await Client.GetAsync($"/api/comments/user/{activeUser.Id}");
        var resLesser = await Client.GetAsync($"/api/comments/user/{lessUser.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(resActive, HttpStatusCode.OK);
        await IntegrationAssertions.AssertStatusAsync(resLesser, HttpStatusCode.OK);
        var commentsActive = await resActive.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var commentsLesser = await resLesser.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(commentsActive);
        Assert.NotNull(commentsLesser);
        Assert.Equal(2, commentsActive.Count);
        Assert.Single(commentsLesser);
        Assert.Equal(comment1.Content, commentsActive[0].Content);
        Assert.Equal(comment2.Content, commentsActive[1].Content);
        Assert.Equal(commentLesser.Content, commentsLesser[0].Content);
    }

    [Fact]
    public async Task GetCommentsByUser_ReturnsFlatList_IncludingSiblingReplies()
    {
        // Arrange
        const string testMethodName = "GetCommentsByUser_Flat";
        var author = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        var otherUser = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Other", 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, author);
        var post = await CreatePostForTest(testMethodName, author.Id);
        var rootComment1 = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var siblingReply1 = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: rootComment1.Id);
        var nestedReply = await CreateCommentForTest(testMethodName, post.Id, index: 4, parentId: siblingReply1.Id);
        var siblingReply2 = await CreateCommentForTest(testMethodName, post.Id, index: 3, parentId: rootComment1.Id);
        var rootComment2 = await CreateCommentForTest(testMethodName, (await CreatePostForTest(testMethodName, author.Id, index: 2)).Id, index: 5);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, otherUser);
        var otherUserRootComment = await CreateCommentForTest(testMethodName, post.Id, index: 11);
        await CreateCommentForTest(testMethodName, post.Id, index: 12, parentId: rootComment1.Id);

        // Act
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, author);
        var resAuthor = await Client.GetAsync($"/api/comments/user/{author.Id}");
        var resOther = await Client.GetAsync($"/api/comments/user/{otherUser.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(resAuthor, HttpStatusCode.OK);
        await IntegrationAssertions.AssertStatusAsync(resOther, HttpStatusCode.OK);

        var authorComments = await resAuthor.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var otherComments = await resOther.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(authorComments);
        Assert.NotNull(otherComments);

        Assert.Equal(5, authorComments.Count);
        foreach (var comment in authorComments) Assert.Empty(comment.Replies);

        var byId = authorComments.ToDictionary(c => c.Id);
        Assert.Contains(rootComment1.Id, byId.Keys);
        Assert.Contains(siblingReply1.Id, byId.Keys);
        Assert.Contains(siblingReply2.Id, byId.Keys);
        Assert.Contains(nestedReply.Id, byId.Keys);
        Assert.Contains(rootComment2.Id, byId.Keys);

        Assert.Null(byId[rootComment1.Id].ParentId);
        Assert.Null(byId[rootComment2.Id].ParentId);

        Assert.Equal(rootComment1.Id, byId[siblingReply1.Id].ParentId);
        Assert.Equal(rootComment1.Id, byId[siblingReply2.Id].ParentId);
        Assert.Equal(siblingReply1.Id, byId[nestedReply.Id].ParentId);

        Assert.Equal(2, otherComments.Count);
        foreach (var comment in otherComments) Assert.Empty(comment.Replies);
        Assert.Contains(otherUserRootComment.Id, otherComments.Select(c => c.Id));
        Assert.DoesNotContain(rootComment1.Id, otherComments.Select(c => c.Id));
        Assert.DoesNotContain(siblingReply1.Id, otherComments.Select(c => c.Id));
    }

    [Fact]
    public async Task GetCommentsByUser_Returns404_WhenUserMissing()
    {
        // Arrange
        const long missingUserId = 9999; // Assuming this user has been deleted while commenting

        // Act
        var res = await Client.GetAsync($"/api/comments/user/{missingUserId}");
        
        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "User not found");
    }

    [Fact]
    public async Task GetCommentsByUser_Returns204_WhenNoComments()
    {
        // Arrange
        const string testMethodName = "GetCommentsByUser_NoComments";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
       
        // Act
        var res = await Client.GetAsync($"/api/comments/user/{user.Id}");
        
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateComment_Returns200_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "UpdatePostOwner";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        string newContent = $"{testMethodName} Updated Content";
        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = newContent 
        };

        // Act
        var res = await Client.PatchAsJsonAsync("/api/comments", req);
        var resDto = await res.Content.ReadFromJsonAsync<CommentPatchResponseDto>();
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);

        Assert.NotNull(resDto);
        Assert.Equal(post.Id, resDto.PostId);
        Assert.Equal(newContent, resDto.Content);

        Assert.NotNull(dto);
        Assert.Equal(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdateComment_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        const string testMethodName = "UpdateComment";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var nonOwner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "NonOwner", 2);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        var post = await CreatePostForTest(testMethodName, owner.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, nonOwner);
        string newContent = $"{testMethodName} Hijacked Content";
        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = newContent 
        };

        // Act
        var res = await Client.PatchAsJsonAsync("/api/comments", req);
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.Unauthorized, "Unauthorized access");

        Assert.NotNull(dto);
        Assert.Equal(comment.Content, dto.Content);
        Assert.NotEqual(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdateComment_Returns400_WhenPostDeletedButCommentRemains()
    {
        // Arrange
        const string testMethodName = "UpdateComment_PostMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = $"{testMethodName} Updated Content"
        };

        // Act
        await Client.DeleteAsync($"/api/posts/{post.Id}");
        var res = await Client.PatchAsJsonAsync("/api/comments", req);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.BadRequest,
            "Post is not reachable. Comments are readonly.");
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(comment.Content, dto.Content);
        Assert.Null(dto.PostId);
        Assert.Equal(post.Id, dto.DeletedPostId);
    }

    [Fact]
    public async Task UpdateComment_Returns404_WhenCommentMissing()
    {
        // Arrange
        const string testMethodName = "UpdateComment_CommentMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long nonExistentCommentId = 9999; // Assuming this comment has been deleted while commenting
        string newContent = $"{testMethodName} Updated Content";
        var req = new CommentPatchRequestDto
        {
            Id = nonExistentCommentId,
            Content = newContent,
        };
        
        // Act
        var res = await Client.PatchAsJsonAsync("/api/comments", req);
        
        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Comment not found");
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteComment_Returns204_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "DeleteComment";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);
        
        // Act
        var res = await Client.DeleteAsync($"/api/comments/{comment.Id}");
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteComment_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        const string testMethodName = "DeleteCommentAuth";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var nonOwner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "NonOwner", 2);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        var post = await CreatePostForTest(testMethodName, owner.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, nonOwner);
        
        // Act
        var res = await Client.DeleteAsync($"/api/comments/{comment.Id}");
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.Unauthorized, "Unauthorized access");
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
        Assert.Equal(comment.Content, dto.Content);
    }

    [Fact]
    public async Task DeleteComment_Returns204_WhenPostAlreadyDeleted()
    {
        // Arrange
        const string testMethodName = "DeleteComment_PostMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        // Act
        var deletePostRes = await Client.DeleteAsync($"/api/posts/{post.Id}");
        var res = await Client.DeleteAsync($"/api/comments/{comment.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(deletePostRes, HttpStatusCode.NoContent);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
        var getRes = await Client.GetAsync($"/api/comments/{comment.Id}");
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteComment_Returns404_WhenCommentMissing()
    {
        // Arrange
        const string testMethodName = "DeleteComment_CommentMissing";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long nonExistentCommentId = 9999; // Assuming this comment has been deleted while commenting
        
        // Act
        var res = await Client.DeleteAsync($"/api/comments/{nonExistentCommentId}");
        
        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Comment not found");
    }
}
