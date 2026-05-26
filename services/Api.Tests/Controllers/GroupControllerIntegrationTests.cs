using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    // --- CREATE ---

    [Fact]
    public async Task CreateGroup_Returns201_AndOwnerIsCreator()
    {
        const string testMethodName = "GroupCreate";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);

        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        Assert.Equal(1, group.MemberCount);

        var membersRes = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");
        Assert.Equal(HttpStatusCode.OK, membersRes.StatusCode);
        var members = await membersRes.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.NotNull(members);
        var single = Assert.Single(members);
        Assert.Equal(GroupRole.Owner, single.Role);
        Assert.Equal(owner.Id, single.UserId);
    }

    [Fact]
    public async Task CreateGroup_Returns401_WhenNotAuthenticated()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var res = await Client.PostAsJsonAsync(GroupIntegrationTestHelpers.GroupsBase, new GroupCreateRequestDto
        {
            Name = "x",
            Description = "y",
            Visibility = GroupVisibility.Private,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- GET ---

    [Fact]
    public async Task GetGroup_Returns404_WhenPrivateAndNonMember()
    {
        const string testMethodName = "GroupPrivateGet";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Group not found", problem.Detail);
    }

    [Fact]
    public async Task GetGroup_Returns200_WhenPublicAndNonMember()
    {
        const string testMethodName = "GroupPublicGet";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Public);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateGroup_Returns200_WhenOwner()
    {
        const string testMethodName = "GroupPatch";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var patched = await Client.PatchAsJsonAsync(GroupIntegrationTestHelpers.GroupsBase, new GroupPatchRequestDto
        {
            Id = group.Id,
            Name = "Renamed",
            Description = "new",
            Visibility = GroupVisibility.Public,
        });

        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var body = await patched.Content.ReadFromJsonAsync<GroupResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Renamed", body.Name);
        Assert.Equal(GroupVisibility.Public, body.Visibility);
    }

    [Fact]
    public async Task UpdateGroup_Returns401_WhenNonMember()
    {
        const string testMethodName = "GroupPatchAuth";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.PatchAsJsonAsync(GroupIntegrationTestHelpers.GroupsBase, new GroupPatchRequestDto
        {
            Id = group.Id,
            Name = "Hacked",
            Description = "x",
            Visibility = GroupVisibility.Public,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized access", problem.Detail);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteGroup_Returns204_WhenOwner()
    {
        const string testMethodName = "GroupDelete";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var get = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_Returns401_WhenNonMember()
    {
        const string testMethodName = "GroupDeleteAuth";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized access", problem.Detail);
    }

    // --- TRANSFER ---

    [Fact]
    public async Task TransferOwnership_Returns400_WhenTargetIsNotMember()
    {
        const string testMethodName = "GroupTransfer";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        var res = await Client.PatchAsJsonAsync($"{GroupIntegrationTestHelpers.GroupsBase}/transfer", new GroupTransferOwnershipRequestDto
        {
            Id = group.Id,
            NewOwnerUserId = stranger.Id,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Target user is not a member of this group.", problem.Detail);
    }
}
