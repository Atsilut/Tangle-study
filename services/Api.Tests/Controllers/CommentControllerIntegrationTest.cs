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

    private async Task<CommentGetResponseDto> CreateCommentForTest(string testMethodName, long postId, long index = 1)
    {
        var req = new CommentCreateRequestDto
        {
            PostId = postId,
            Content = $"{testMethodName} Test " + index.ToString()
        };
        var create = await _client.PostAsJsonAsync("/api/comments", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var getAll = await _client.GetAsync($"/api/comments/post/{postId}");
        var all = await getAll.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        return all!.Single(c => c.Content == req.Content && c.PostId == postId);
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
        var content = testMethodName + "Test";

        var listRes = await _client.GetAsync($"/api/comments/post/{post.Id}");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var allComments = await listRes.Content.ReadFromJsonAsync<List<CommentGetResponseDto>>();
        var created = allComments!.Single(c => c.Content == content);

        // Act
        var res = await _client.GetAsync($"/api/comments/{created.PostId}");
        var dto = await res.Content.ReadFromJsonAsync<CommentGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(content, dto.Content);
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
}
