using Users.Domain;
using Users.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Users.Tests.Infrastructure;

internal static class ServiceTestHelpers
{
    public static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())])),
    };

    public static async Task<User> CreateUserAsync(
        FakeUserRepository repo,
        string nickname = "test",
        string password = "password")
    {
        var user = new User($"{nickname}@test.com", password, nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
