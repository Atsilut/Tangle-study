using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testUserPassword = "testtest123!";
    private readonly string testUserNickname = "old";

    private async Task<UserGetResponseDto> CreateUserForTest()
    {
        var req = new UserCreateRequestDto
        {
            Email = $"{Guid.NewGuid()}@test.com",
            Password = testUserPassword,
            Nickname = testUserNickname,
        };
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.First(u => u.Email == req.Email);
    }


    [Fact]
    public async Task GetUserById_Return200_WhenFound()
    {
        // Arrange
        var created = await CreateUserForTest();

        // Act
        var res = await Client.GetAsync("/api/users/" + created.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetUserById_Return404_WhenMissing()
    {
        // Arrange
        const long missingUserId = 123456;

        // Act
        var res = await Client.GetAsync("/api/users/" + missingUserId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Return200_WhenValidRequest()
    {
        // Arrange
        var created = await CreateUserForTest();
        const string newNickname = "new";
        var updatedAtBefore = created.UpdatedAt;

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>();
        Assert.NotNull(patched);
        Assert.Equal(newNickname, patched.Nickname);
        Assert.True(updatedAtBefore < patched.UpdatedAt);
    }

    [Fact]
    public async Task UpdateUser_Return404_WhenMissing()
    {
        // Arrange
        var created = await CreateUserForTest();
        const string newNickname = "new";
        const long userIdOffset = 123456;
        var wrongUserId = created.Id + userIdOffset;

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(wrongUserId, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Return204_WhenFound()
    {
        // Arrange
        var created = await CreateUserForTest();

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var found = await Client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, found.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Return404_WhenMissing()
    {
        // Arrange
        var created = await CreateUserForTest();
        const long userIdOffset = 123456;
        var wrongUserId = created.Id + userIdOffset;

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{wrongUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
