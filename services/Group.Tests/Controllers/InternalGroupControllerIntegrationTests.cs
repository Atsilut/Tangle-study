using System.Net;
using System.Net.Http.Json;
using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Controllers;

[Collection(GroupIntegrationTestCollection.Name)]
public sealed class InternalGroupControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task EnsureGroupExists_Returns404_WithGroupNotFound_WhenMissing()
    {
        GroupTestAuthHelpers.LoginAsInternal(Client);

        var res = await Client.PostAsync(
            "/internal/group/999/exists",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
    }

    [Fact]
    public async Task ValidateGroupMember_UsesCallerNotFoundMessage()
    {
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, "owner");
        var stranger = GroupIntegrationTestHelpers.CreateUser(Factory, "stranger");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        GroupTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"/internal/group/{group.Id}/members/{stranger.Id}/validate",
            new { notFoundMessage = "Group not found" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
    }

    [Fact]
    public async Task ValidateGroupMember_DefaultsToGroupNotFound_WhenBodyOmitted()
    {
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, "owner");
        var stranger = GroupIntegrationTestHelpers.CreateUser(Factory, "stranger");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        GroupTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsync(
            $"/internal/group/{group.Id}/members/{stranger.Id}/validate",
            content: null,
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
    }

    [Fact]
    public async Task ValidateGroupMembers_UsesCallerErrorMessage()
    {
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, "owner");
        var stranger = GroupIntegrationTestHelpers.CreateUser(Factory, "stranger");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        GroupTestAuthHelpers.LoginAsInternal(Client);
        var res = await Client.PostAsJsonAsync(
            $"/internal/group/{group.Id}/members/validate",
            new { userIds = new[] { stranger.Id }, errorMessage = "All participants must be members of this group" },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.BadRequest,
            "All participants must be members of this group");
    }

    [Fact]
    public async Task ValidateBoardView_ReturnsNoContent_WhenMemberCanView()
    {
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, "owner");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            owner,
            GroupVisibility.Public,
            GroupJoinPolicy.Open);
        GroupIntegrationTestHelpers.LoginAs(Client, owner);
        var boardRes = await Client.PostAsJsonAsync(
            $"/api/groups/{group.Id}/boards",
            new GroupBoardCreateRequestDto
            {
                Name = "general",
                Description = "desc",
                Visibility = BoardVisibility.ForAll,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, boardRes.StatusCode);
        var board = await boardRes.Content.ReadFromJsonAsync<GroupBoardGetResponseDto>(
            TestContext.Current.CancellationToken);

        GroupTestAuthHelpers.LoginAsInternal(Client, owner.Id);
        var res = await Client.PostAsync(
            $"/internal/group/{group.Id}/boards/{board!.Id}/validate-view",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task DetachOnDeletion_RemovesMembership_AndDeletesSoleOwnerGroup()
    {
        var owner = GroupIntegrationTestHelpers.CreateUser(Factory, "owner");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);

        GroupTestAuthHelpers.LoginAsInternal(Client);
        var detachRes = await Client.PostAsync(
            $"/internal/group/users/{owner.Id}/detach-on-deletion",
            content: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, detachRes.StatusCode);

        GroupTestAuthHelpers.LoginAsInternal(Client);
        var existsRes = await Client.PostAsync(
            $"/internal/group/{group.Id}/exists",
            content: null,
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertProblemDetailAsync(
            existsRes,
            HttpStatusCode.NotFound,
            "Group not found");

        Assert.Contains(group.Id, FakeCommunity.DeletedGroupIds);
        Assert.Contains(group.Id, FakeLocation.EndedSessionGroupIds);
    }
}
