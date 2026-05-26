using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class UserBlockServiceUnitTests
{
    [Fact]
    public async Task BlockUser_CreatesBlock()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var blocker = await CreateUserAsync(graph.UserRepository, "blocker");
        var blocked = await CreateUserAsync(graph.UserRepository, "blocked");
        http.HttpContext = ContextFor(blocker.Id);

        await graph.UserBlockService.BlockUserAsync(new UserBlockCreateRequestDto { BlockedUserId = blocked.Id });

        Assert.True(await graph.UserBlockRepository.ExistsUserBlockAsync(blocker.Id, blocked.Id));
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new User($"{nickname}@test.com", "password", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
