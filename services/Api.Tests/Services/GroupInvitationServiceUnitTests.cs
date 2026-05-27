using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupInvitationServiceUnitTests
{
    [Fact]
    public async Task Invite_CreatesPendingInvitation()
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
            JoinPolicy = GroupJoinPolicy.InvitationOnly,
        });

        // Act
        var result = await graph.GroupInvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = stranger.Id });

        // Assert
        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(await graph.GroupInvitationRepository.GetPendingForUserAsync(group.Id, stranger.Id));
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
