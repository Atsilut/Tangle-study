using System.Net;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Controllers;

public sealed class UserGroupDeletionIntegrationTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    [Fact]
    public async Task DeleteUser_AsSoleGroupOwner_DeletesGroup()
    {
        // Arrange
        var scenario = CreateScenario("user_del_sole");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, scenario.Owner);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Owner.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        scenario.LoginAs(GroupActorRole.Stranger);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await AssertGroupNotFoundAsync(getGroup);
    }

    [Fact]
    public async Task DeleteUser_AsGroupOwnerWithAdmin_TransfersOwnership()
    {
        // Arrange
        var scenario = CreateScenario("user_del_xfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Owner.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        GroupIntegrationTestHelpers.LoginAs(Client, scenario.Admin);
        var members = await scenario.GetMembersAsync(group.Id);
        Assert.DoesNotContain(members, m => m.UserId == scenario.Owner.Id);
        var owner = Assert.Single(members, m => m.Role == GroupRole.Owner);
        Assert.Equal(scenario.Admin.Id, owner.UserId);
    }

    [Fact]
    public async Task DeleteUser_AsMember_RemovesMembershipOnly()
    {
        // Arrange
        var scenario = CreateScenario("user_del_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);

        // Act
        GatewayTestAuthHelpers.LoginAsInternal(Client);
        var delete = await Client.PostAsync($"/internal/group/users/{scenario.Member.Id}/detach-on-deletion", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        GroupIntegrationTestHelpers.LoginAs(Client, scenario.Owner);
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getGroup, HttpStatusCode.OK);
    }
}
