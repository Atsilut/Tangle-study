using System.Net;
using Group.Entities;
using Group.Tests.Infrastructure;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;

namespace Group.Tests.Controllers;

public sealed class UserGroupDeletionIntegrationTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    [Fact]
    public async Task DeleteUser_AsSoleGroupOwner_DeletesGroup()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"user_del_sole");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, scenario.Owner);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Owner.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        scenario.LoginAs(GroupActorRole.Stranger);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertProblemDetailAsync(getGroup, HttpStatusCode.NotFound, "Group not found");
    }

    [Fact]
    public async Task DeleteUser_AsGroupOwnerWithAdmin_TransfersOwnership()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"user_del_xfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Owner.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        GatewayTestAuthHelpers.LoginAs(Client, scenario.Admin.Id);
        var members = await scenario.GetMembersAsync(group.Id);
        Assert.DoesNotContain(members, m => m.UserId == scenario.Owner.Id);
        var owner = Assert.Single(members, m => m.Role == GroupRole.Owner);
        Assert.Equal(scenario.Admin.Id, owner.UserId);
    }

    [Fact]
    public async Task DeleteUser_AsMember_RemovesMembershipOnly()
    {
        // Arrange
        var scenario = GroupIntegrationScenario.Create(Client, Factory,"user_del_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Member.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        GatewayTestAuthHelpers.LoginAs(Client, scenario.Owner.Id);
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getGroup, HttpStatusCode.OK);
    }
}
