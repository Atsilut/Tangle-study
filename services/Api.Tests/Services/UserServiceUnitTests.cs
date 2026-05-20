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

        // Act
        var dto = await service.GetUserByIdAsync(12345);

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
        var user = new User(email: "a@a.com", password: "password", nickname: "old");
        await repo.CreateUserAsync(user);

        // Act
        var res = await service.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, "new"));

        // Assert
        Assert.NotNull(res);
        Assert.Equal("new", res!.Nickname);

        var reloaded = await repo.GetUserByIdAsync(user.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("new", reloaded!.Nickname);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_Throws_WhenUserMissing()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.UpdateUserDetailAsync(new UserPatchRequestDto(1, "new")));
    }

    [Fact]
    public async Task DeleteUserAsync_DeleteUser()
    {
        // Arrange
        var repo = new FakeUserRepository();
        var service = new UserService(repo);
        var user = new User(email: "a@a.com", password: "password", nickname: "old");
        await repo.CreateUserAsync(user);

        // Act
        await service.DeleteUserAsync(user.Id);

        // Assert
        var deleted = await repo.GetUserByIdAsync(user.Id);
        Assert.Null(deleted);
    }
}
