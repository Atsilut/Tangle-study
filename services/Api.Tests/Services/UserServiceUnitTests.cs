using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Api.Global.Exceptions;

namespace Api.Tests.Service;

public sealed class UserServiceUnitTests
{
    // --- GET ---

    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();
        var service = graph.UserService;
        const long missingUserId = 12345;

        // Act
        var dto = await service.GetUserByIdAsync(missingUserId);

        // Assert
        Assert.Null(dto);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();
        var repo = graph.UserRepository;
        var service = graph.UserService;
        const string email = "a@a.com";
        const string password = "password";
        const string oldNickname = "old";
        const string newNickname = "new";
        var user = new User(email: email, password: password, nickname: oldNickname);
        await repo.CreateUserAsync(user);

        // Act
        var res = await service.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, newNickname));

        // Assert
        Assert.NotNull(res);
        Assert.Equal(newNickname, res!.Nickname);

        var reloaded = await repo.GetUserByIdAsync(user.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(newNickname, reloaded!.Nickname);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_Throws_WhenUserMissing()
    {
        // Arrange
        var service = DomainServiceTestFactory.Create().UserService;
        const long missingUserId = 1;
        const string newNickname = "new";

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.UpdateUserDetailAsync(new UserPatchRequestDto(missingUserId, newNickname)));
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesSameNickname()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();
        var repo = graph.UserRepository;
        var service = graph.UserService;
        const string email = "a@a.com";
        const string password = "password";
        const string nickname = "old";
        var user = new User(email: email, password: password, nickname: nickname);
        await repo.CreateUserAsync(user);

        // Act
        var res = await service.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, nickname));

        // Assert
        Assert.NotNull(res);
        Assert.Equal(nickname, res!.Nickname);

        var reloaded = await repo.GetUserByIdAsync(user.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(nickname, reloaded!.Nickname);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_ThrowsEntityAlreadyExists_WhenNicknameAlreadyExists()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();
        var repo = graph.UserRepository;
        var service = graph.UserService;
        const string duplicateNickname = "taken";
        var owner = new User(email: "a@a.com", password: "password", nickname: "owner");
        var existingUser = new User(email: "b@b.com", password: "password", nickname: duplicateNickname);
        await repo.CreateUserAsync(owner);
        await repo.CreateUserAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            service.UpdateUserDetailAsync(new UserPatchRequestDto(owner.Id, duplicateNickname)));
    }

    [Fact]
    public async Task UpdateUserDetailAsync_ThrowsUnauthorized_WhenUserDoesNotOwnRequest()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create(new FakeHttpContextAccessor("2"));
        var repo = graph.UserRepository;
        var service = graph.UserService;
        var owner = new User(email: "a@a.com", password: "password", nickname: "owner");
        var attacker = new User(email: "b@b.com", password: "password", nickname: "attacker");
        await repo.CreateUserAsync(owner);
        await repo.CreateUserAsync(attacker);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateUserDetailAsync(new UserPatchRequestDto(owner.Id, "new")));
    }

    [Fact]
    public async Task UpdatePrivacySettingsAsync_UpdatesFriendsListVisibility()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var repo = graph.UserRepository;
        var service = graph.UserService;
        var user = new User("a@a.com", "password", "nick");
        await repo.CreateUserAsync(user);

        var res = await service.UpdatePrivacySettingsAsync(
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Public });

        Assert.Equal(FriendsListVisibility.Public, res.FriendsListVisibility);
        var reloaded = await repo.GetUserByIdAsync(user.Id);
        Assert.Equal(FriendsListVisibility.Public, reloaded!.FriendsListVisibility);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteUserAsync_DeleteUser()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();
        var repo = graph.UserRepository;
        var postRepo = graph.PostRepository;
        var service = graph.UserService;
        const string email = "a@a.com";
        const string password = "password";
        const string nickname = "old";
        var user = new User(email: email, password: password, nickname: nickname);
        await repo.CreateUserAsync(user);

        var post = new Api.Domain.Posts.Domain.Post(user.Id, "title", "content");
        await postRepo.CreatePostAsync(post);

        // Act
        await service.DeleteUserAsync(user.Id);

        // Assert
        var deleted = await repo.GetUserByIdAsync(user.Id);
        Assert.Null(deleted);
        var orphanedPost = await postRepo.GetPostByIdAsync(post.Id);
        Assert.NotNull(orphanedPost);
        Assert.Null(orphanedPost.UserId);
        Assert.Equal(user.Id, orphanedPost.DeletedUserId);
    }

    [Fact]
    public async Task DeleteUserAsync_ThrowsUnauthorized_WhenUserDoesNotOwnTarget()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create(new FakeHttpContextAccessor("2"));
        var repo = graph.UserRepository;
        var service = graph.UserService;
        var owner = new User(email: "a@a.com", password: "password", nickname: "owner");
        var attacker = new User(email: "b@b.com", password: "password", nickname: "attacker");
        await repo.CreateUserAsync(owner);
        await repo.CreateUserAsync(attacker);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteUserAsync(owner.Id));
    }
}
