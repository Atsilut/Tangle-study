using System.Net;
using System.Net.Http.Json;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testPassword = "testpass123!";

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
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    private async Task LoginAs(UserGetResponseDto user, string password)
    {
        var req = new LoginRequestDto
        {
            Email = user.Email,
            Password = password
        };
        var res = await Client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var loginRes = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginRes);

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }

    // --- CREATE ---

    [Fact]
    public async Task CreatePost_Returns201()
    {
        // Arrange
        const string testMethodName = "PostCreate";
        const string title = "my title";
        const string content = "my content";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var req = new PostCreateRequestDto { Title = title, Content = content };

        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req);

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreatePost_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;
        const string title = "title";
        const string content = "content";
        var req = new PostCreateRequestDto { Title = title, Content = content };

        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "valid content")]
    [InlineData("valid title", "")]
    public async Task CreatePost_Returns400_WhenInvalidRequest(string title, string content)
    {
        // Arrange
        const string testMethodName = "PostCreateWithInvalidRequest";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var req = new PostCreateRequestDto { Title = title, Content = content };
        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req);
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // --- GET ---

    [Fact]
    public async Task GetAllPosts_Returns200_WithPosts()
    {
        // Arrange
        const string testMethodName = "GetAllPosts";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        const string title = "test title";
        const string content = "test content";
        const string titleSuffix1 = "1";
        const string titleSuffix2 = "2";
        const string contentSuffix1 = " 111";
        const string contentSuffix2 = " 222";
        var createReq1 = new PostCreateRequestDto { Title = title + titleSuffix1, Content = content + contentSuffix1 };
        var createReq2 = new PostCreateRequestDto { Title = title + titleSuffix2, Content = content + contentSuffix2 };
        var createRes1 = await Client.PostAsJsonAsync("/api/posts", createReq1);
        Assert.Equal(HttpStatusCode.Created, createRes1.StatusCode);
        var createRes2 = await Client.PostAsJsonAsync("/api/posts", createReq2);
        Assert.Equal(HttpStatusCode.Created, createRes2.StatusCode);

        // Act
        var res = await Client.GetAsync("/api/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.Title == title + titleSuffix1 && p.Content == content + contentSuffix1);
        Assert.Contains(list, p => p.Title == title + titleSuffix2 && p.Content == content + contentSuffix2);
    }

    [Fact]
    public async Task GetAllPosts_Returns204_WhenNoPosts()
    {
        // Act
        var res = await Client.GetAsync("/api/posts");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetPostById_Returns200_WhenPostExists()
    {
        // Arrange
        const string testMethodName = "GetPostById";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        const string title = "test title";
        const string content = "test content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(post => post.Title == title);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(title, dto.Title);
        Assert.Equal(content, dto.Content);
        Assert.Equal(user.Id, dto.AuthorId);
        Assert.Equal(user.Nickname, dto.AuthorNickname);
    }

    [Fact]
    public async Task GetPostById_Returns404_WhenPostMissing()
    {
        // Arrange
        const long missingPostId = 999999999999;

        // Act
        var res = await Client.GetAsync($"/api/posts/{missingPostId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns200_WithAuthorPosts()
    {
        // Arrange
        const string testMethodName = "GetPostsByNick";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        const string title = "nickname lookup title";
        const string content = "nickname lookup content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        Client.DefaultRequestHeaders.Authorization = null;

        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var list = await getRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(list);
        var post = Assert.Single(list);
        Assert.Equal(title, post.Title);
        Assert.Equal(content, post.Content);
        Assert.Equal(user.Id, post.AuthorId);
        Assert.Equal(user.Nickname, post.AuthorNickname);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns204_WhenUserHasNoPosts()
    {
        // Arrange
        const string testMethodName = "GetPostsByNickEmpty";
        var user = await CreateUserForTest(testMethodName, testPassword);
        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, getRes.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns204_WhenNicknameUnknown()
    {
        // Arrange
        const string unknownNicknamePath = "DefinitelyNoSuchUserNickname99999";

        // Act
        var getRes = await Client.GetAsync("/api/posts/nickname/" + unknownNicknamePath);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, getRes.StatusCode);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdatePost_Returns200_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "PostPatch";
        const string originalTitle = "original";
        const string originalBody = "original body";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var createReq = new PostCreateRequestDto { Title = originalTitle, Content = originalBody };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == originalTitle);
        var updatedAtBefore = created.UpdatedAt;

        const string newTitle = "updated title";
        const string newContent = "updated body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };

        // Act
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq);
        var patchDto = await patchRes.Content.ReadFromJsonAsync<PostPatchResponseDto>();
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        Assert.NotNull(patchDto);
        Assert.Equal(newTitle, patchDto.Title);
        Assert.Equal(newContent, patchDto.Content);
        Assert.True(updatedAtBefore < patchDto.UpdatedAt);

        Assert.NotNull(dto);
        Assert.Equal(newTitle, dto.Title);
        Assert.Equal(newContent, dto.Content);
        Assert.True(updatedAtBefore < dto.UpdatedAt);
    }

    [Fact]
    public async Task UpdatePost_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        const string testMethodName = "PostPatchAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var nonOwner = await CreateUserForTest(testMethodName + "NonOwner", testPassword);

        await LoginAs(owner, testPassword);
        const string title = "owners post";
        const string content = "hands off";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == title);

        await LoginAs(nonOwner, testPassword);
        const string newTitle = "hijacked title";
        const string newContent = "hijacked body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };

        // Act
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, patchRes.StatusCode);

        await LoginAs(owner, testPassword);
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(title, dto.Title);
        Assert.NotEqual(newTitle, dto.Title);
        Assert.Equal(content, dto.Content);
        Assert.NotEqual(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdatePost_Returns404_WhenPostMissing()
    {
        // Arrange
        const string testMethodName = "PostPatchMissing";
        const long missingPostId = 999999999999;
        const string notApplicable = "n/a";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var patchReq = new PostPatchRequestDto
        {
            Id = missingPostId,
            Title = notApplicable,
            Content = notApplicable,
        };

        // Act
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patchRes.StatusCode);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeletePost_Returns204_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "PostDelete";
        const string deleteTitle = "to delete";
        const string deleteContent = "gone soon";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var createReq = new PostCreateRequestDto { Title = deleteTitle, Content = deleteContent };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == deleteTitle);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{created.Id}");
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task DeletePost_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        const string testMethodName = "PostDeleteAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var other = await CreateUserForTest(testMethodName + "Other", testPassword);

        await LoginAs(owner, testPassword);
        const string title = "do not delete me";
        const string content = "still here";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == title);

        await LoginAs(other, testPassword);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, deleteRes.StatusCode);

        await LoginAs(owner, testPassword);
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(title, dto.Title);
        Assert.Equal(content, dto.Content);
    }

    [Fact]
    public async Task DeletePost_Returns404_WhenPostMissing()
    {
        // Arrange
        const string testMethodName = "PostDeleteMissing";
        const long missingPostId = 999999999999;
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{missingPostId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, deleteRes.StatusCode);
    }
}
