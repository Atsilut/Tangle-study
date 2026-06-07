using System.Net;
using Api.Domain.Groups.Domain;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class UserGroupDeletionIntegrationTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    [Fact]
    public async Task DeleteUser_AsSoleGroupOwner_DeletesGroup()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("user_del_sole");
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, scenario.Owner);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, scenario.Owner);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{scenario.Owner.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await AssertGroupNotFoundAsync(getGroup);
    }

    [Fact]
    public async Task DeleteUser_AsGroupOwnerWithAdmin_TransfersOwnership()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("user_del_xfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, scenario.Owner);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{scenario.Owner.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, scenario.Admin);
        var members = await scenario.GetMembersAsync(group.Id);
        Assert.DoesNotContain(members, m => m.UserId == scenario.Owner.Id);
        var owner = Assert.Single(members, m => m.Role == GroupRole.Owner);
        Assert.Equal(scenario.Admin.Id, owner.UserId);
    }

    [Fact]
    public async Task DeleteUser_AsMember_RemovesMembershipOnly()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("user_del_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        await GroupIntegrationTestHelpers.LoginAsAsync(Client, scenario.Member);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{scenario.Member.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(delete, HttpStatusCode.NoContent);

        await GroupIntegrationTestHelpers.LoginAsAsync(Client, scenario.Owner);
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
        var getGroup = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getGroup, HttpStatusCode.OK);
    }
}
