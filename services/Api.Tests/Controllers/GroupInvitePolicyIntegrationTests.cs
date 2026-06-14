using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupInvitePolicyIntegrationTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    [Fact]
    public async Task CreateGroup_DefaultsInvitePolicyToAdminsOnly()
    {
        var owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(
            Client, nameof(CreateGroup_DefaultsInvitePolicyToAdminsOnly), 1);
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(Client, owner);
        Assert.Equal(GroupInvitePolicy.AdminsOnly, group.InvitePolicy);
    }

    [Fact]
    public async Task Invite_MemberSucceeds_WhenInvitePolicyForAll()
    {
        var scenario = await CreateScenarioAsync(nameof(Invite_MemberSucceeds_WhenInvitePolicyForAll));
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            scenario.Owner,
            GroupVisibility.Public,
            GroupJoinPolicy.InvitationOnly,
            GroupInvitePolicy.ForAll);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, scenario.Member.Id, GroupRole.Member);

        await scenario.LoginAsAsync(GroupActorRole.Member);
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
    }

    [Fact]
    public async Task Invite_MemberReturnsUnauthorized_WhenInvitePolicyAdminsOnly()
    {
        var scenario = await CreateScenarioAsync(nameof(Invite_MemberReturnsUnauthorized_WhenInvitePolicyAdminsOnly));
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeAdmin: true, includeMember: true);

        await scenario.LoginAsAsync(GroupActorRole.Member);
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListGroupInvitations_MemberCanList_WhenInvitePolicyForAll()
    {
        var scenario = await CreateScenarioAsync(nameof(ListGroupInvitations_MemberCanList_WhenInvitePolicyForAll));
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            Client,
            scenario.Owner,
            GroupVisibility.Public,
            GroupJoinPolicy.InvitationOnly,
            GroupInvitePolicy.ForAll);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, scenario.Member.Id, GroupRole.Member);
        await scenario.InviteStrangerAsync(group.Id, GroupActorRole.Member);

        await scenario.LoginAsAsync(GroupActorRole.Member);
        var res = await Client.GetAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var invitations = await res.Content.ReadFromJsonAsync<List<GroupInvitationGroupListItemDto>>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(invitations);
        Assert.Single(invitations);
        Assert.Equal(scenario.Stranger.Id, invitations[0].InviteeId);
        Assert.Equal(scenario.Member.Id, invitations[0].InviterId);
    }

    [Fact]
    public async Task UpdateGroup_PersistsInvitePolicy()
    {
        var scenario = await CreateScenarioAsync(nameof(UpdateGroup_PersistsInvitePolicy));
        var group = await scenario.SetupInvitationOnlyGroupAsync();

        await scenario.LoginAsAsync(GroupActorRole.Owner);
        var res = await Client.PatchAsJsonAsync(
            GroupIntegrationTestHelpers.GroupsBase,
            new GroupPatchRequestDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Visibility = group.Visibility,
                JoinPolicy = group.JoinPolicy,
                InvitePolicy = GroupInvitePolicy.ForAll,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<GroupGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(GroupInvitePolicy.ForAll, updated.InvitePolicy);
    }
}
