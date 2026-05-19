using Api.Domain.Comments.Dto;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CommentControllerIntegrationTest : IDisposable
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private readonly string testPassword = "testpass123!";

    public CommentControllerIntegrationTest(PostgresTestcontainerFixture postgres)
    {
        _factory = new ApiWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, string password = "testpass123!", long index = 1)
    {
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = $"{testMethodName}User" + index.ToString();
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await _client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await _client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    private async Task<PostGetResponseDto> CreatePostForTest(string testMethodName, long userId, long index = 1)
    {
        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} Post Title " + index.ToString(),
            Content = $"{testMethodName} Post Content " + index.ToString()
        };
        var create = await _client.PostAsJsonAsync("/api/posts", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var getAll = await _client.GetAsync("/api/posts");
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
        var create = await _client.PostAsJsonAsync("/api/comments", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var getAll = await _client.GetAsync($"/api/comments/post/{postId}");
        var all = await getAll.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var found = FindCommentByContent(all!, req.Content);
        Assert.NotNull(found);
        Assert.Equal(postId, found.PostId);
        if (parentId.HasValue)
            Assert.Equal(parentId.Value, found.ParentId);
        return found;
    }

    private CommentGetResponseDto? FindCommentByContent(
        IEnumerable<CommentGetResponseDto> comments,
        string content)
    {
        foreach (var comment in comments)
        {
            if (comment.Content == content)
                return comment;
            var inReplies = FindCommentByContent(comment.Replies, content);
            if (inReplies != null)
                return inReplies;
        }
        return null;
    }

    private async Task LoginAs(UserGetResponseDto user, string password)
    {
        var req = new LoginRequestDto
        {
            Email = user.Email,
            Password = password
        };
        var res = await _client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var loginRes = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginRes);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }

    private async Task DeleteAllPostsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Posts.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CreateComment_Returns201()
    {
        // Arrange
        var testMethodName = "CreateComment";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        //Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Returns401_IfNotLoggedIn()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var req = new CommentCreateRequestDto
        {
            PostId = 1,
            Content = "Unauthorized Comment"
        };
        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);
        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Return400_IfPostNotFound()
    {
        // Arrange
        var testMethodName = "CreateComment_PostNotFound";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        const long nonExistentPostId = 9999; // Assuming this post has been deleted while commenting
        var req = new CommentCreateRequestDto
        {
            PostId = nonExistentPostId,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByPost_ReturnsComments()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "GetCommentsByPost";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment1 = await CreateCommentForTest(testMethodName, post.Id, 1);
        var comment2 = await CreateCommentForTest(testMethodName, post.Id, 2);

        // Act
        var res = await _client.GetAsync($"/api/comments/post/{post.Id}");
        
        //Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var comments = await res.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments.Count);
        Assert.Equal(comment1.Content, comments[0].Content);
        Assert.Equal(comment2.Content, comments[1].Content);
    }

    [Fact]
    public async Task CreateComment_WithParentId_Returns201()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "CreateNestedComment";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var rootComment = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            ParentId = rootComment.Id,
            Content = $"{testMethodName} Reply"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var getByPostRes = await _client.GetAsync($"/api/comments/post/{post.Id}");
        var commentTree = await getByPostRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        Assert.NotNull(commentTree);
        var rootDto = commentTree.Single(c => c.Id == rootComment.Id);
        Assert.Single(rootDto.Replies);
        Assert.Equal(req.Content, rootDto.Replies[0].Content);
        Assert.Equal(rootComment.Id, rootDto.Replies[0].ParentId);
    }

    [Fact]
    public async Task CreateComment_Return400_IfParentNotFound()
    {
        // Arrange
        var testMethodName = "CreateNestedComment_ParentMissing";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long missingParentId = 9999;
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            ParentId = missingParentId,
            Content = $"{testMethodName} Orphan Reply"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Return400_IfParentOnDifferentPost()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "CreateNestedComment_ParentWrongPost";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
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
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByPost_ReturnsNestedTree()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "GetCommentsByPost_Nested";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var rootComment = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var replyComment = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: rootComment.Id);
        var nestedReply = await CreateCommentForTest(testMethodName, post.Id, index: 3, parentId: replyComment.Id);
        await CreateCommentForTest(testMethodName, post.Id, index: 4);

        // Act
        var res = await _client.GetAsync($"/api/comments/post/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
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
    public async Task GetCommentById_ReturnsFlatReply_WithParentId()
    {
        // Arrange
        var testMethodName = "GetCommentById_Nested";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var root = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var reply = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: root.Id);

        // Act
        var res = await _client.GetAsync($"/api/comments/{reply.Id}");
        var dto = await res.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(reply.Content, dto.Content);
        Assert.Equal(root.Id, dto.ParentId);
        Assert.Empty(dto.Replies);
    }

    [Fact]
    public async Task GetCommentsByPost_Returns404_WhenPostMissing()
    {
        // Arrange
        const long missingPostId = 9999; // Assuming this post has been deleted while commenting

        // Act
        var res = await _client.GetAsync($"/api/comments/post/{missingPostId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByPost_Returns204_WhenNoComments()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "GetCommentsByPost_NoComments";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        
        // Act
        var res = await _client.GetAsync($"/api/comments/post/{post.Id}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByUser_ReturnsComments()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "GetCommentsByUser";
        var activeUser = await CreateUserForTest(testMethodName, testPassword);
        var lessUser = await CreateUserForTest(testMethodName + "Lesser", testPassword);
        await LoginAs(activeUser, testPassword);
        var post1 = await CreatePostForTest(testMethodName, activeUser.Id, 1);
        var post2 = await CreatePostForTest(testMethodName, activeUser.Id, 2);
        var comment1 = await CreateCommentForTest(testMethodName, post1.Id, 1);
        var comment2 = await CreateCommentForTest(testMethodName, post2.Id, 2);
        await LoginAs(lessUser, testPassword);
        var commentLesser = await CreateCommentForTest(testMethodName, post2.Id, 11);

        // Act
        var resActive = await _client.GetAsync($"/api/comments/user/{activeUser.Id}");
        var resLesser = await _client.GetAsync($"/api/comments/user/{lessUser.Id}");

        //Assert
        Assert.Equal(HttpStatusCode.OK, resActive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resLesser.StatusCode);
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
        await DeleteAllPostsAsync();
        var testMethodName = "GetCommentsByUser_Flat";
        var author = await CreateUserForTest(testMethodName, testPassword);
        var otherUser = await CreateUserForTest(testMethodName + "Other", testPassword, index: 2);
        await LoginAs(author, testPassword);
        var post = await CreatePostForTest(testMethodName, author.Id);
        var rootComment1 = await CreateCommentForTest(testMethodName, post.Id, index: 1);
        var siblingReply1 = await CreateCommentForTest(testMethodName, post.Id, index: 2, parentId: rootComment1.Id);
        var nestedReply = await CreateCommentForTest(testMethodName, post.Id, index: 4, parentId: siblingReply1.Id);
        var siblingReply2 = await CreateCommentForTest(testMethodName, post.Id, index: 3, parentId: rootComment1.Id);
        var rootComment2 = await CreateCommentForTest(testMethodName, (await CreatePostForTest(testMethodName, author.Id, index: 2)).Id, index: 5);

        await LoginAs(otherUser, testPassword);
        var otherUserRootComment = await CreateCommentForTest(testMethodName, post.Id, index: 11);
        await CreateCommentForTest(testMethodName, post.Id, index: 12, parentId: rootComment1.Id);

        // Act
        await LoginAs(author, testPassword);
        var resAuthor = await _client.GetAsync($"/api/comments/user/{author.Id}");
        var resOther = await _client.GetAsync($"/api/comments/user/{otherUser.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resAuthor.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resOther.StatusCode);

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
    public async Task GetCommentsByUser_Return404_WhenUserMissing()
    {
        // Arrange
        const long missingUserId = 9999; // Assuming this user has been deleted while commenting
        // Act

        var res = await _client.GetAsync($"/api/comments/user/{missingUserId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentsByUser_Return204_WhenNoComments()
    {
        // Arrange
        var testMethodName = "GetCommentsByUser_NoComments";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
       
        // Act
        var res = await _client.GetAsync($"/api/comments/user/{user.Id}");
        
        //Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetCommentById_Returns200_WhenPostExists()
    {
        // Arrange
        var testMethodName = "GetCommentById";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        // Act
        var res = await _client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await res.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(comment.Content, dto.Content);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(post.Id, dto.PostId);
    }

    [Fact]
    public async Task GetCommentById_Return404_WhenCommentMissing()
    {
        // Arrange
        const long missingCommentId = 9999; // Assuming this comment has been deleted while commenting
        
        // Act
        var res = await _client.GetAsync($"/api/comments/{missingCommentId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Return200_WhenLoggedInAsOwner()
    {
        // Arrange
        var testMethodName = "UpdatePostOwner";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        var newContent = $"{testMethodName} Updated Content";
        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = newContent 
        };

        // Act
        var res = await _client.PatchAsJsonAsync($"/api/comments", req);
        var resDto = await res.Content.ReadFromJsonAsync<CommentPatchResponseDto>();
        var getRes = await _client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

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
        var testMethodName = "UpdateComment";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var nonOwner = await CreateUserForTest(testMethodName + "NonOwner", testPassword, 2);

        await LoginAs(owner, testPassword);
        var post = await CreatePostForTest(testMethodName, owner.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        await LoginAs(nonOwner, testPassword);
        var newContent = $"{testMethodName} Hijacked Content";
        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = newContent 
        };

        // Act
        var res = await _client.PatchAsJsonAsync($"/api/comments", req);
        var getRes = await _client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        Assert.NotNull(dto);
        Assert.Equal(comment.Content, dto.Content);
        Assert.NotEqual(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdateComment_Return404_WhenPostMissing()
    {
        // Arrange
        var testMethodName = "UpdateComment_PostMissing";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        var req = new CommentPatchRequestDto
        { 
            Id = comment.Id,
            Content = $"{testMethodName} Updated Content" 
        };

        // Act
        await _client.DeleteAsync($"/api/posts/{post.Id}"); // Delete the post while commenting
        var res = await _client.PatchAsJsonAsync($"/api/comments", req);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Returns404_WhenCommentMissing()
    {
        // Arrange
        var testMethodName = "UpdateComment_CommentMissing";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long nonExistentCommentId = 9999; // Assuming this comment has been deleted while commenting
        var newContent = $"{testMethodName} Updated Content";
        var req = new CommentPatchRequestDto
        {
            Id = nonExistentCommentId,
            Content = newContent,
        };
        
        // Act
        var res = await _client.PatchAsJsonAsync($"/api/comments", req);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_Returns204_WhenLoggedInAsOwner()
    {
        // Arrange
        var testMethodName = "DeleteComment";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);
        
        // Act
        var res = await _client.DeleteAsync($"/api/comments/{comment.Id}");
        var getRes = await _client.GetAsync($"/api/comments/{comment.Id}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        var testMethodName = "DeleteCommentAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var nonOwner = await CreateUserForTest(testMethodName + "NonOwner", testPassword, 2);
        await LoginAs(owner, testPassword);
        var post = await CreatePostForTest(testMethodName, owner.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);
        await LoginAs(nonOwner, testPassword);
        
        // Act
        var res = await _client.DeleteAsync($"/api/comments/{comment.Id}");
        var getRes = await _client.GetAsync($"/api/comments/{comment.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        Assert.Equal(comment.Content, dto.Content);
    }

    [Fact]
    public async Task DeleteComment_Returns404_WhenPostMissing()
    {
        // Arrange
        var testMethodName = "DeleteComment_PostMissing";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var comment = await CreateCommentForTest(testMethodName, post.Id);

        // Act
        var deletePostRes = await _client.DeleteAsync($"/api/posts/{post.Id}"); // Delete the post while deleting comment
        var res = await _client.DeleteAsync($"/api/comments/{comment.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deletePostRes.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_Returns404_WhenCommentMissing()
    {
        // Arrange
        var testMethodName = "DeleteComment_CommentMissing";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        const long nonExistentCommentId = 9999; // Assuming this comment has been deleted while commenting
        
        // Act
        var res = await _client.DeleteAsync($"/api/comments/{nonExistentCommentId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
