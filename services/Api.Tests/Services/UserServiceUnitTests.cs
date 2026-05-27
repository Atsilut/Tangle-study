using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

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
        http.HttpContext = ContextFor(user.Id);

        // Act
        var res = await graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, "new"));

        // Assert
        Assert.NotNull(res);
        Assert.Equal("new", res!.Nickname);
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
        http.HttpContext = ContextFor(user.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto(other.Id, "hijacked")));
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };
}
