using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupBlacklistJoinIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    public static TheoryData<GroupActorRole, BlacklistAdminAction, GroupExpectedOutcome> AdminAuthorizationData =>
        new()
        {
            { GroupActorRole.Owner, BlacklistAdminAction.Add, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Admin, BlacklistAdminAction.Remove, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, BlacklistAdminAction.Remove, GroupExpectedOutcome.Ok },
        };

    [Theory]
    [MemberData(nameof(AdminAuthorizationData))]
    public async Task BlacklistAdminAuthorization_Matrix(
        GroupActorRole caller,
        BlacklistAdminAction action,
        GroupExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"blacklist_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);

        if (action == BlacklistAdminAction.Add)
        {
            await scenario.LoginAsAsync(caller);

            // Act
            var res = await Client.PostAsJsonAsync(
                $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/blacklist",
                new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id }, TestContext.Current.CancellationToken);

            // Assert
            if (expected == GroupExpectedOutcome.Ok) await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
            else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
            return;
        }

        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        await scenario.LoginAsAsync(caller);

        // Act
        var removeRes = await Client.DeleteAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/blacklist/{scenario.Stranger.Id}", TestContext.Current.CancellationToken);

        // Assert
        if (expected == GroupExpectedOutcome.Ok) await IntegrationAssertions.AssertStatusAsync(removeRes, HttpStatusCode.NoContent);
        else await IntegrationAssertions.AssertStatusAsync(removeRes, OutcomeStatus(expected));
    }

    [Fact]
    public async Task Join_WhenBlacklisted_Returns400()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("blj01");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Blacklist_Add_KicksExistingMember()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("blj06");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        await Client.PostAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/join", null, TestContext.Current.CancellationToken);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);

        // Act
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        // Assert
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, false);
    }

    [Fact]
    public async Task AcceptInvitation_AfterBlacklist_Returns404()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("blj04");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.InvitationOnly);
        var invitation = await GroupIntegrationTestHelpers.SeedInvitationAsync(
            Factory, group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Blacklist_Add_WhenSelf_Returns400()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("bla04");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = scenario.Owner.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }
}
