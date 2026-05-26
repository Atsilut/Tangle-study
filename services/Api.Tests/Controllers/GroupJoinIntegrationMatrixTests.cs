using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupJoinIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    public static TheoryData<GroupJoinPolicy, JoinPolicyOperation, JoinPolicyRouteOutcome> PolicyRoutingMatrixData =>
        new()
        {
            { GroupJoinPolicy.Open, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.MemberAdded },
            { GroupJoinPolicy.Open, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.UseJoinEndpoint },
            { GroupJoinPolicy.Requestable, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.RequiresApplication },
            { GroupJoinPolicy.Requestable, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.ApplicationCreated },
            { GroupJoinPolicy.InvitationOnly, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.InvitationOnly },
            { GroupJoinPolicy.InvitationOnly, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.InvitationOnly },
        };

    [Theory]
    [MemberData(nameof(PolicyRoutingMatrixData))]
    public async Task JoinPolicyRouting_Matrix(
        GroupJoinPolicy joinPolicy,
        JoinPolicyOperation operation,
        JoinPolicyRouteOutcome expected)
    {
        var scenario = await CreateScenarioAsync($"join_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: joinPolicy);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        if (operation == JoinPolicyOperation.Join)
        {
            var joinRes = await Client.PostAsync(
                $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null);
            if (expected == JoinPolicyRouteOutcome.MemberAdded)
            {
                await IntegrationAssertions.AssertStatusAsync(joinRes, HttpStatusCode.OK);
                await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
            }
            else
            {
                await IntegrationAssertions.AssertStatusAsync(joinRes, HttpStatusCode.BadRequest);
                await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, false);
            }
            return;
        }

        var applyRes = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null);
        if (expected == JoinPolicyRouteOutcome.ApplicationCreated)
        {
            await IntegrationAssertions.AssertStatusAsync(applyRes, HttpStatusCode.Created);
            await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, false);
        }
        else
        {
            await IntegrationAssertions.AssertStatusAsync(applyRes, HttpStatusCode.BadRequest);
            await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, false);
        }
    }

    [Fact]
    public async Task Join_WhenAlreadyMember_Returns409()
    {
        var scenario = await CreateScenarioAsync("jp_p01");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_WhenAlreadyMember_Returns409()
    {
        var scenario = await CreateScenarioAsync("jp_p02");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await scenario.LoginAsAsync(GroupActorRole.Member);

        var res = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Join_WithPendingInvitation_AddsMember()
    {
        var scenario = await CreateScenarioAsync("jp_p05");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await GroupIntegrationTestHelpers.SeedInvitationAsync(
            Factory, group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task Apply_WithPendingInvitation_ReturnsOkAndAddsMember()
    {
        var scenario = await CreateScenarioAsync("jp_p08");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await GroupIntegrationTestHelpers.SeedInvitationAsync(
            Factory, group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        var res = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task Join_WhenGroupMissing_Returns404()
    {
        var scenario = await CreateScenarioAsync("jp_p10");
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/99999/join", null);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }
}
