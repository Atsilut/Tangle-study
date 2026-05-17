using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserControllerIntegrationTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private readonly string testUserPassword = "testtest123!";
    private readonly string testUserNickname = "old";

    public UserControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    {
        _factory = new ApiWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<UserGetResponseDto> CreateUserForTest()
    {
        var req = new UserCreateRequestDto
        {
            Email = $"{Guid.NewGuid()}@test.com",
            Password = testUserPassword,
            Nickname = testUserNickname,
        };
        var create = await _client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await _client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.First(u => u.Email == req.Email);
    }


    [Fact]
    public async Task GetUserById_ReturnsUser_WhenFound()
    {
        // Arrange
        var created = await CreateUserForTest();

        // Act
        var res = await _client.GetAsync("/api/users/" + created.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetUserById_Returns404_WhenMissing()
    {
        // Arrange
        const long missingUserId = 123456;

        // Act
        var res = await _client.GetAsync("/api/users/" + missingUserId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_UpdatesNickname_ValidRequest()
    {
        // Arrange
        var created = await CreateUserForTest();
        var newNickname = "new";

        // Act
        var patch = await _client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>();
        Assert.NotNull(patched);
        Assert.Equal(newNickname, patched.Nickname);
    }

    [Fact]
    public async Task UpdateUser_Return404_WhenMissing()
    {
        // Arrange
        var created = await CreateUserForTest();
        var newNickname = "new";
        var wrongUserId = created.Id + 123456;

        // Act
        var patch = await _client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(wrongUserId, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_DeletesUser_WhenFound()
    {
        // Arrange
        var created = await CreateUserForTest();

        // Act
        var delete = await _client.DeleteAsync($"/api/users/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var found = await _client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, found.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Return404_WhenMissing()
    {
        // Arrange
        var created = await CreateUserForTest();
        var wrongUserId = created.Id + 123456;

        // Act
        var delete = await _client.DeleteAsync($"/api/users/{wrongUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
