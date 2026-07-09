using Users.Domain;
using Users.Dto;
using Users.Tests.Infrastructure;
using Tangle.AspNetCore.Exceptions;
using Tangle.TestSupport.Integration;

namespace Users.Tests.Services;

public sealed class UserServiceUnitTests
{
    [Fact]
    public async Task GetUserByIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();

        // Act
        var dto = await graph.UserService.GetUserByIdAsync(12345);

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetUserByIdOrThrowAsync_Throws_WhenMissing()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            graph.UserService.GetUserByIdOrThrowAsync(12345));
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = new User("a@a.com", "password", "old");
        await graph.UserRepository.CreateUserAsync(user);
        http.HttpContext = FakeHttpContextAccessor.ContextFor(user.Id);

        // Act
        var res = await graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto { Id = user.Id, Nickname = "new" });

        // Assert
        Assert.NotNull(res);
        Assert.Equal("new", res.Nickname);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_OtherUsersId_ThrowsUnauthorized()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = new User("a@a.com", "password", "old");
        var other = new User("b@b.com", "password", "other");
        await graph.UserRepository.CreateUserAsync(user);
        await graph.UserRepository.CreateUserAsync(other);
        http.HttpContext = FakeHttpContextAccessor.ContextFor(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto { Id = other.Id, Nickname = "hijacked" }));
    }

    [Fact]
    public async Task DeleteUserAsync_LeavesUserIntact_WhenDetachFails()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = new User("a@a.com", "password", "nick");
        await graph.UserRepository.CreateUserAsync(user);
        http.HttpContext = FakeHttpContextAccessor.ContextFor(user.Id);
        graph.CommunityClient.DetachFailure = new InvalidOperationException("detach failed");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => graph.UserService.DeleteUserAsync(user.Id));
        var stillThere = await graph.UserRepository.GetUserByIdAsync(user.Id);
        Assert.NotNull(stillThere);
        Assert.Empty(graph.CommunityClient.DetachedUserIds);
    }
}
