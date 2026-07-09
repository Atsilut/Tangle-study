using System.Net;
using System.Net.Http.Json;
using Tangle.TestSupport.Auth;
using Users.Domain;
using Users.Dto;
using Users.Tests.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class UserControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
{
    // --- GET ---

    [Fact]
    public async Task GetUserById_Returns200_WhenFound()
    {
        // Arrange
        const string testMethodName = "GetUserById";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);

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

    [Fact]
    public async Task GetUserById_DoesNotExposeEmail_WhenViewerIsNotSelf()
    {
        // Arrange
        const string testMethodName = "GetUserByIdEmailPrivacy";
        var owner = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var viewer = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Viewer");

        // Act
        var res = await Client.GetAsync("/api/users/" + owner.Id, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var user = await res.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Null(user.Email);

        await UsersTestAuthHelpers.LoginAsAsync(Client, viewer);
        var viewerRes = await Client.GetAsync("/api/users/" + owner.Id, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(viewerRes, HttpStatusCode.OK);
        var asViewer = await viewerRes.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(asViewer);
        Assert.Null(asViewer.Email);

        await UsersTestAuthHelpers.LoginAsAsync(Client, owner);
        var selfRes = await Client.GetAsync("/api/users/" + owner.Id, TestContext.Current.CancellationToken);
        var self = await selfRes.Content.ReadFromJsonAsync<UserGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(self);
        Assert.Equal(UsersTestAuthHelpers.GetTestEmail(owner.Id), self.Email);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateUser_Returns200_WhenValidRequest()
    {
        // Arrange
        const string testMethodName = "UserPatch";
        const string newNickname = "new";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);
        var updatedAtBefore = created.UpdatedAt;

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = created.Id, Nickname = newNickname }, TestContext.Current.CancellationToken);

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
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = created.Id, Nickname = newNickname }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        const string testMethodName = "UserPatchUnauth";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        GatewayTestAuthHelpers.ClearAuth(Client);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = created.Id, Nickname = "new" }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(patch, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_Returns200_WhenSameNickname()
    {
        // Arrange
        const string testMethodName = "UserPatchSameNickname";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = created.Id, Nickname = created.Nickname }, TestContext.Current.CancellationToken);

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
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        var existingUser = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Existing");
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = created.Id, Nickname = existingUser.Nickname }, TestContext.Current.CancellationToken);

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
        var owner = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var attacker = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Attacker");
        await UsersTestAuthHelpers.LoginAsAsync(Client, attacker);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto { Id = owner.Id, Nickname = "hacked" }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(patch, HttpStatusCode.Unauthorized, "Unauthorized access");
    }

    [Fact]
    public async Task UpdatePrivacy_Returns200_AndUpdatesFriendsListVisibility()
    {
        // Arrange
        const string testMethodName = "UserPrivacy";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);

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
        Assert.NotNull(user);
        Assert.Equal(FriendsListVisibility.Public, user.FriendsListVisibility);
    }

    [Fact]
    public async Task UpdatePrivacy_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        GatewayTestAuthHelpers.ClearAuth(Client);

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
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);
        Assert.Contains(created.Id, Factory.FakeChatClient.DetachedUserIds);
        Assert.Contains(created.Id, Factory.FakeGroupClient.DetachedUserIds);
        Assert.Contains(created.Id, Factory.FakeSocialClient.DetachedUserIds);
        Assert.Contains(created.Id, Factory.FakeCommunityClient.DetachedUserIds);
        Assert.Contains(created.Id, Factory.FakeMediaClient.DetachedUploaderIds);
        Assert.Contains(created.Id, Factory.FakeLocationClient.DetachedUserIds);

        var found = await Client.GetAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(found, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Returns401_WhenLoggedInUserWasDeleted()
    {
        // Arrange
        const string testMethodName = "UserDeleteMissing";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);
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
        var owner = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Owner");
        var attacker = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName + "Attacker");
        await UsersTestAuthHelpers.LoginAsAsync(Client, attacker);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{owner.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(delete, HttpStatusCode.Unauthorized, "Unauthorized access");
    }

    [Fact]
    public async Task DeleteUser_LeavesUserIntact_WhenDetachFails()
    {
        // Arrange
        const string testMethodName = "UserDeleteDetachFail";
        var created = await UsersTestAuthHelpers.CreateUserForTestAsync(Client, testMethodName);
        await UsersTestAuthHelpers.LoginAsAsync(Client, created);
        Factory.FakeCommunityClient.DetachFailure = new HttpRequestException("community detach failed");

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, delete.StatusCode);
        var found = await Client.GetAsync($"/api/users/{created.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(found, HttpStatusCode.OK);
        Assert.Empty(Factory.FakeCommunityClient.DetachedUserIds);
    }
}
