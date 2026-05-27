using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupServiceUnitTests
{
    [Fact]
    public async Task CreateGroup_AddsCreatorAsOwner()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", owner.Id.ToString()) })),
        };

        // Act
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Devs",
            Description = "desc",
            Visibility = GroupVisibility.Private,
        });

        // Assert
        Assert.Equal(1, group.MemberCount);
        var member = await graph.GroupMemberRepository.GetMemberAsync(group.Id, owner.Id);
        Assert.NotNull(member);
        Assert.Equal(GroupRole.Owner, member!.Role);
    }

    [Fact]
    public async Task GetGroup_ThrowsNotFound_WhenPrivateAndNotMember()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var stranger = await CreateUserAsync(graph.UserRepository, "stranger");
        http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", owner.Id.ToString()) })),
        };
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "Private",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        http.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", stranger.Id.ToString()) })),
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => graph.GroupService.GetGroupAsync(group.Id));
    }

    private static async Task<Api.Domain.Users.Domain.User> CreateUserAsync(FakeUserRepository repo, string nickname)
    {
        var user = new Api.Domain.Users.Domain.User($"{nickname}@test.com", "password", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
