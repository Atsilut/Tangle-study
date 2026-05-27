using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupApplicationServiceUnitTests
{
    [Fact]
    public async Task Apply_CreatesPendingApplication()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var stranger = await CreateUserAsync(graph.UserRepository, "stranger");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Public,
            JoinPolicy = GroupJoinPolicy.Requestable,
        });
        http.HttpContext = ContextFor(stranger.Id);

        // Act
        var result = await graph.GroupApplicationService.ApplyAsync(group.Id);

        // Assert
        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(await graph.GroupApplicationRepository.GetPendingForUserAsync(group.Id, stranger.Id));
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<Api.Domain.Users.Domain.User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new Api.Domain.Users.Domain.User($"{nickname}@test.com", "password", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
