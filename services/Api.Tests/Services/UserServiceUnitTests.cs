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
        var graph = DomainServiceTestFactory.Create();
        var dto = await graph.UserService.GetUserByIdAsync(12345);
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetUserByIdOrThrowAsync_Throws_WhenMissing()
    {
        var graph = DomainServiceTestFactory.Create();
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            graph.UserService.GetUserByIdOrThrowAsync(12345));
    }

    [Fact]
    public async Task UpdateUserDetailAsync_UpdatesNickname()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = new User("a@a.com", "password", "old");
        await graph.UserRepository.CreateUserAsync(user);
        http.HttpContext = ContextFor(user.Id);

        var res = await graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto(user.Id, "new"));

        Assert.NotNull(res);
        Assert.Equal("new", res!.Nickname);
    }

    [Fact]
    public async Task UpdateUserDetailAsync_OtherUsersId_ThrowsUnauthorized()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = new User("a@a.com", "password", "old");
        var other = new User("b@b.com", "password", "other");
        await graph.UserRepository.CreateUserAsync(user);
        await graph.UserRepository.CreateUserAsync(other);
        http.HttpContext = ContextFor(user.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.UserService.UpdateUserDetailAsync(new UserPatchRequestDto(other.Id, "hijacked")));
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };
}
