using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    // --- GET ---

    [Fact]
    public async Task GetUserById_Returns200_WhenFound()
    {
        // Arrange
        const string testMethodName = "GetUserById";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);

        // Act
        var res = await Client.GetAsync("/api/users/" + created.Id, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserById_Returns404_WhenMissing()
    {
        // Arrange
        const long missingUserId = 123456;

        // Act
        var res = await Client.GetAsync("/api/users/" + missingUserId, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateUser_Returns200_WhenValidRequest()
    {
        // Arrange
        const string testMethodName = "UserPatch";
        const string newNickname = "new";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);
        var updatedAtBefore = created.UpdatedAt;

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, newNickname), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(patched);
        Assert.Equal(newNickname, patched.Nickname);
        Assert.True(updatedAtBefore < patched.UpdatedAt);
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenLoggedInUserWasDeleted()
    {
        // Arrange
        const string testMethodName = "UserPatchMissing";
        const string newNickname = "new";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, newNickname), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        const string testMethodName = "UserPatchUnauth";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, "new"), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_Returns200_WhenSameNickname()
    {
        // Arrange
        const string testMethodName = "UserPatchSameNickname";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, created.Nickname), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(patched);
        Assert.Equal(created.Nickname, patched.Nickname);
    }

    [Fact]
    public async Task UpdateUser_Returns409_WhenNicknameAlreadyExists()
    {
        // Arrange
        const string testMethodName = "UserPatchDuplicateNickname";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        var existingUser = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Existing");
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, existingUser.Nickname), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(
            patch,
            HttpStatusCode.Conflict,
            $"A user with nickname '{existingUser.Nickname}' already exists.");
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenUpdatingOtherUser()
    {
        // Arrange
        const string testMethodName = "UserPatchAuth";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var attacker = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Attacker");
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, attacker);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(owner.Id, "hacked"), TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(patch, HttpStatusCode.Unauthorized, "Unauthorized access");
    }

    [Fact]
    public async Task UpdatePrivacy_Returns200_AndUpdatesFriendsListVisibility()
    {
        // Arrange
        const string testMethodName = "UserPrivacy";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Public }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<UserPrivacySettingsResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(FriendsListVisibility.Public, body.FriendsListVisibility);

        var get = await Client.GetAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        var user = await get.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.Equal(FriendsListVisibility.Public, user!.FriendsListVisibility);
    }

    [Fact]
    public async Task UpdatePrivacy_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Private }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.Unauthorized);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteUser_Returns204_WhenFound()
    {
        // Arrange
        const string testMethodName = "UserDelete";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        var found = await Client.GetAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(found, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Returns401_WhenLoggedInUserWasDeleted()
    {
        // Arrange
        const string testMethodName = "UserDeleteMissing";
        var created = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, created);
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        // Act
        delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUser_Returns401_WhenDeletingOtherUser()
    {
        // Arrange
        const string testMethodName = "UserDeleteAuth";
        var owner = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var attacker = await IntegrationTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Attacker");
        await IntegrationTestAuthHelpers.LoginAsAsync(Client, attacker);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{owner.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(delete, HttpStatusCode.Unauthorized, "Unauthorized access");
    }
}
