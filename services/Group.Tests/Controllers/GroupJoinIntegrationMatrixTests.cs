using System.Net;
using Group.Entities;
using Group.Tests.Infrastructure;
using Tangle.TestSupport.Integration;

namespace Group.Tests.Controllers;

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
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,$"join_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: joinPolicy);
        scenario.LoginAs(GroupActorRole.Stranger);

        if (operation == JoinPolicyOperation.Join)
        {
            // Act
            var joinRes = await Client.PostAsync(
                $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null, TestContext.Current.CancellationToken);

            // Assert
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

        // Act
        var applyRes = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null, TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"jp_p01");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: GroupJoinPolicy.Open);
        scenario.LoginAs(GroupActorRole.Owner);

        // Act
        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apply_WhenAlreadyMember_Returns409()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"jp_p02");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        scenario.LoginAs(GroupActorRole.Member);

        // Act
        var res = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Join_WithPendingInvitation_AddsMember()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"jp_p05");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await GroupIntegrationTestHelpers.SeedInvitationAsync(
            Factory, group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        // Act
        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task Apply_WithPendingInvitation_ReturnsOkAndAddsMember()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"jp_p08");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await GroupIntegrationTestHelpers.SeedInvitationAsync(
            Factory, group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        // Act
        var res = await Client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task Join_WhenGroupMissing_Returns404()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"jp_p10");
        scenario.LoginAs(GroupActorRole.Stranger);

        // Act
        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/99999/join", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");
    }
}
