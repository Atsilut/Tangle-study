using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupMemberControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task GetMembers_Returns404_WhenPrivateAndStranger()
    {
        const string testMethodName = "GmPrivateStranger";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Private);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Group not found", problem.Detail);
    }

    [Fact]
    public async Task GetMembers_Returns200_WhenPublicAndStranger()
    {
        const string testMethodName = "GmPublicStranger";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner, GroupVisibility.Public);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, stranger);

        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var members = await res.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.NotNull(members);
        Assert.Single(members);
    }

    [Fact]
    public async Task RemoveMember_Returns204_WhenMemberLeavesSelf()
    {
        const string testMethodName = "GmSelfLeave";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, member);

        var res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{member.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_Returns200_WhenOwnerPromotesMemberToAdmin()
    {
        const string testMethodName = "GmPromote";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, member.Id, GroupRole.Member);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);

        var res = await Client.PatchAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{member.Id}",
            new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<GroupMemberResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(GroupRole.Admin, body.Role);
    }

    [Fact]
    public async Task RemoveMember_Returns401_WhenAdminKicksAnotherAdmin()
    {
        const string testMethodName = "GmAdminKickAdmin";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var adminA = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var adminB = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 3);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, adminA.Id, GroupRole.Admin);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, adminB.Id, GroupRole.Admin);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, adminA);

        var res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{adminB.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized access", problem.Detail);
    }

    [Fact]
    public async Task TransferOwnership_Returns200_AndSwapsRoles()
    {
        const string testMethodName = "GmTransfer";
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 1);
        var heir = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, testMethodName, 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, heir.Id, GroupRole.Member);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);

        var transfer = await Client.PatchAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/transfer",
            new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = heir.Id });

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var membersRes = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");
        Assert.Equal(HttpStatusCode.OK, membersRes.StatusCode);
        var members = await membersRes.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.NotNull(members);
        Assert.Equal(GroupRole.Owner, members.Single(m => m.UserId == heir.Id).Role);
        Assert.Equal(GroupRole.Admin, members.Single(m => m.UserId == owner.Id).Role);
    }
}
