using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class GroupJoinPolicyIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Join_OpenGroup_ReturnsOkAndAddsMember()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Join_OpenGroup_ReturnsOkAndAddsMember), 1);
        var joiner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Join_OpenGroup_ReturnsOkAndAddsMember), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client, owner, GroupVisibility.Public, GroupJoinPolicy.Open);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, joiner);
        var join = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, owner);
        var members = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members");
        var list = await members.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
        Assert.Contains(list!, m => m.UserId == joiner.Id);
    }

    [Fact]
    public async Task Join_RequestableGroup_Returns400()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Join_RequestableGroup_Returns400), 1);
        var joiner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Join_RequestableGroup_Returns400), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client, owner, GroupVisibility.Public, GroupJoinPolicy.Requestable);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, joiner);
        var join = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null);
        Assert.Equal(HttpStatusCode.BadRequest, join.StatusCode);
    }

    [Fact]
    public async Task Apply_OpenGroup_Returns400()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_OpenGroup_Returns400), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_OpenGroup_Returns400), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client, owner, GroupVisibility.Public, GroupJoinPolicy.Open);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null);
        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
    }

    [Fact]
    public async Task Apply_InvitationOnlyGroup_Returns400()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_InvitationOnlyGroup_Returns400), 1);
        var applicant = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(Apply_InvitationOnlyGroup_Returns400), 2);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client, owner, GroupVisibility.Public, GroupJoinPolicy.InvitationOnly);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, applicant);
        var apply = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null);
        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_ReturnsJoinPolicyInResponse()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(Client, nameof(CreateGroup_ReturnsJoinPolicyInResponse), 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client, owner, GroupVisibility.Private, GroupJoinPolicy.InvitationOnly);

        Assert.Equal(GroupJoinPolicy.InvitationOnly, group.JoinPolicy);
    }
}
