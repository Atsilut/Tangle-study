using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Service;
using Api.Tests.Repositories;
using Api.Global.Exceptions;

namespace Api.Tests.Service;

public sealed class UserServiceUnitTests
{
    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
        const long missingUserId = 12345;

        // Act
        var dto = await service.GetUserByIdAsync(missingUserId);

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
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
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
        const long missingUserId = 1;
        const string newNickname = "new";

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.UpdateUserDetailAsync(new UserPatchRequestDto(missingUserId, newNickname)));
    }

    [Fact]
    public async Task DeleteUserAsync_DeleteUser()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
        const string email = "a@a.com";
        const string password = "password";
        const string nickname = "old";
        var user = new User(email: email, password: password, nickname: nickname);
        await repo.CreateUserAsync(user);

        // Act
        await service.DeleteUserAsync(user.Id);

        // Assert
        var deleted = await repo.GetUserByIdAsync(user.Id);
        Assert.Null(deleted);
    }
}
