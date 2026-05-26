using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupMembershipServiceUnitTests
{
    [Fact]
    public async Task HandleUserDeletionAsync_DeletesGroup_WhenOwnerIsSoleMember()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });

        await graph.GroupMembershipService.HandleUserDeletionAsync(owner.Id);

        Assert.False(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
        Assert.Empty(await graph.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    [Fact]
    public async Task HandleUserDeletionAsync_TransfersOwnershipToAdmin_WhenOwnerDeleted()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var admin = await CreateUserAsync(graph.UserRepository, "admin");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, admin.Id, GroupRole.Admin);

        await graph.GroupMembershipService.HandleUserDeletionAsync(owner.Id);

        Assert.Null(await graph.GroupMemberRepository.GetMemberAsync(group.Id, owner.Id));
        var adminMember = await graph.GroupMemberRepository.GetMemberAsync(group.Id, admin.Id);
        Assert.NotNull(adminMember);
        Assert.Equal(GroupRole.Owner, adminMember.Role);
        Assert.True(await graph.GroupRepository.ExistsGroupByIdAsync(group.Id));
    }

    [Fact]
    public async Task GetMembers_ReturnsMembers_WhenCallerIsMember()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var member = await CreateUserAsync(graph.UserRepository, "member");
        http.HttpContext = ContextFor(owner.Id);
        var group = await graph.GroupService.CreateGroupAsync(new GroupCreateRequestDto
        {
            Name = "G",
            Description = "d",
            Visibility = GroupVisibility.Private,
        });
        await graph.GroupMembershipService.AddMemberInternalAsync(group.Id, member.Id, GroupRole.Member);
        http.HttpContext = ContextFor(member.Id);

        var members = await graph.GroupMembershipService.GetMembersAsync(group.Id);

        Assert.NotNull(members);
        Assert.True(members!.Count >= 2);
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
