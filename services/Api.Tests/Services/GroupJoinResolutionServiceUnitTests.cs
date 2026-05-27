using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupJoinResolutionServiceUnitTests
{
    [Fact]
    public async Task CreateMembershipFromJoinRequests_AddsMemberRole()
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
            JoinPolicy = GroupJoinPolicy.Open,
        });

        // Act
        await graph.GroupJoinResolutionService.CreateMembershipFromJoinRequestsAsync(group.Id, stranger.Id);

        // Assert
        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, stranger.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Member, member!.Role);
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
