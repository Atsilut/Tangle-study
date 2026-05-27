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
    // --- CREATE ---

    [Fact]
    public async Task CreatePost_Returns201()
    {
        // Arrange
        const string testMethodName = "PostCreate";
        const string title = "my title";
        const string content = "my content";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var req = new PostCreateRequestDto { Title = title, Content = content };

        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "valid content")]
    [InlineData("valid title", "")]
    public async Task CreatePost_Returns400_WhenInvalidRequest(string title, string content)
    {
        // Arrange
        const string testMethodName = "PostCreateWithInvalidRequest";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var req = new PostCreateRequestDto { Title = title, Content = content };
        // Act
        var res = await Client.PostAsJsonAsync("/api/posts", req);
        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    // --- GET ---

    [Fact]
    public async Task GetAllPosts_Returns200_WithPosts()
    {
        // Arrange
        const string testMethodName = "GetAllPosts";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        const string title = "test title";
        const string content = "test content";
        const string titleSuffix1 = "1";
        const string titleSuffix2 = "2";
        const string contentSuffix1 = " 111";
        const string contentSuffix2 = " 222";
        var createReq1 = new PostCreateRequestDto { Title = title + titleSuffix1, Content = content + contentSuffix1 };
        var createReq2 = new PostCreateRequestDto { Title = title + titleSuffix2, Content = content + contentSuffix2 };
        var createRes1 = await Client.PostAsJsonAsync("/api/posts", createReq1);
        await IntegrationAssertions.AssertStatusAsync(createRes1, HttpStatusCode.Created);
        var createRes2 = await Client.PostAsJsonAsync("/api/posts", createReq2);
        await IntegrationAssertions.AssertStatusAsync(createRes2, HttpStatusCode.Created);

        // Act
        var res = await Client.GetAsync("/api/posts");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);

        var list = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.Title == title + titleSuffix1 && p.Content == content + contentSuffix1);
        Assert.Contains(list, p => p.Title == title + titleSuffix2 && p.Content == content + contentSuffix2);
    }

    [Fact]
    public async Task GetAllPosts_Returns204_WhenNoPosts()
    {
        // Arrange
        // (empty database from test fixture)

        // Act
        var res = await Client.GetAsync("/api/posts");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetPostById_Returns200_WhenPostExists()
    {
        // Arrange
        const string testMethodName = "GetPostById";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        const string title = "test title";
        const string content = "test content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        var listRes = await Client.GetAsync("/api/posts");
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(post => post.Title == title);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();

        // Assert
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
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
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns200_WithAuthorPosts()
    {
        // Arrange
        const string testMethodName = "GetPostsByNick";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        const string title = "nickname lookup title";
        const string content = "nickname lookup content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        Client.DefaultRequestHeaders.Authorization = null;

        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);

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
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await Client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns204_WhenNicknameUnknown()
    {
        // Arrange
        const string unknownNicknamePath = "DefinitelyNoSuchUserNickname99999";

        // Act
        var getRes = await Client.GetAsync("/api/posts/nickname/" + unknownNicknamePath);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NoContent);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdatePost_Returns200_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "PostPatch";
        const string originalTitle = "original";
        const string originalBody = "original body";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createReq = new PostCreateRequestDto { Title = originalTitle, Content = originalBody };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

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
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);

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
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var nonOwner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "NonOwner");

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        const string title = "owners post";
        const string content = "hands off";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == title);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, nonOwner);
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
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.Unauthorized);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
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
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var patchReq = new PostPatchRequestDto
        {
            Id = missingPostId,
            Title = notApplicable,
            Content = notApplicable,
        };

        // Act
        var patchRes = await Client.PatchAsJsonAsync("/api/posts", patchReq);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.NotFound);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeletePost_Returns204_WhenLoggedInAsOwner()
    {
        // Arrange
        const string testMethodName = "PostDelete";
        const string deleteTitle = "to delete";
        const string deleteContent = "gone soon";
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);
        var createReq = new PostCreateRequestDto { Title = deleteTitle, Content = deleteContent };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == deleteTitle);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{created.Id}");
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.NoContent);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePost_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        const string testMethodName = "PostDeleteAuth";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var other = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Other");

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        const string title = "do not delete me";
        const string content = "still here";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await Client.PostAsJsonAsync("/api/posts", createReq);
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);

        var listRes = await Client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == title);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, other);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{created.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.Unauthorized);

        await IntegrationTestAuthHelpers.LoginAsAsync(Client, owner);
        var getRes = await Client.GetAsync($"/api/posts/{created.Id}");
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.OK);
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
        var user = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, user);

        // Act
        var deleteRes = await Client.DeleteAsync($"/api/posts/{missingPostId}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.NotFound);
    }
}
