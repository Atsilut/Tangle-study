using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupJoinMatrixTests
{
    // --- Join policy routing matrix ---

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
    public async Task JoinAsync_RoutesByJoinPolicy_Matrix(
        GroupJoinPolicy joinPolicy,
        JoinPolicyOperation operation,
        JoinPolicyRouteOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"join_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: joinPolicy);
        scenario.LoginAs(GroupActorRole.Stranger);

        if (operation == JoinPolicyOperation.Join)
        {
            if (expected == JoinPolicyRouteOutcome.MemberAdded)
            {
                await scenario.JoinService.JoinAsync(group.Id);
                Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
                await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
                Assert.Empty(await scenario.ApplicationRepository.GetPendingByGroupAsync(group.Id));
            }
            else
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => scenario.JoinService.JoinAsync(group.Id));
                Assert.Contains(
                    expected == JoinPolicyRouteOutcome.RequiresApplication ? "application" : "invitation",
                    ex.Message,
                    StringComparison.OrdinalIgnoreCase);
                Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
            }

            return;
        }

        if (expected == JoinPolicyRouteOutcome.ApplicationCreated)
        {
            var result = await scenario.ApplicationService.ApplyAsync(group.Id);
            Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
            Assert.NotNull(result.Application);
            Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
            Assert.NotNull(await scenario.ApplicationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
        }
        else
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => scenario.ApplicationService.ApplyAsync(group.Id));
            Assert.Contains(
                expected == JoinPolicyRouteOutcome.UseJoinEndpoint ? "join endpoint" : "invitation",
                ex.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        }
    }

    // --- Join policy facts ---

    [Fact]
    public async Task CreateGroup_PersistsJoinPolicy()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_r00");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: GroupJoinPolicy.Open);

        Assert.Equal(GroupJoinPolicy.Open, group.JoinPolicy);
    }

    [Fact]
    public async Task JoinAsync_WhenAlreadyMember_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p01");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: false,
            includeMember: false,
            joinPolicy: GroupJoinPolicy.Open);
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => scenario.JoinService.JoinAsync(group.Id));
        Assert.Contains("already a member", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_WhenAlreadyMember_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p02");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        scenario.LoginAs(GroupActorRole.Member);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => scenario.ApplicationService.ApplyAsync(group.Id));
    }

    [Fact]
    public async Task JoinAsync_WhenBlacklisted_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p03");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.JoinService.JoinAsync(group.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task ApplyAsync_WhenBlacklisted_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p04");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.ApplicationService.ApplyAsync(group.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JoinAsync_WithPendingInvitation_ClearsArtifactsAndAddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p05");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.JoinService.JoinAsync(group.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JoinAsync_WithPendingApplication_ClearsArtifactsAndAddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p06");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.JoinService.JoinAsync(group.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JoinAsync_WithIgnoredInvitation_ClearsRowAndAddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p07");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id, isPending: false);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.JoinService.JoinAsync(group.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task ApplyAsync_WithPendingInvitation_CreatesMembershipFromReciprocalInvitation()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p08");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        var result = await scenario.ApplicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, result.Outcome);
        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task ApplyAsync_WhenDuplicatePending_ReturnsCreatedWithoutSecondRow()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p09");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.ApplicationService.ApplyAsync(group.Id);
        var second = await scenario.ApplicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, second.Outcome);
        Assert.Single(await scenario.ApplicationRepository.GetPendingByGroupAsync(group.Id));
    }

    [Fact]
    public async Task JoinAsync_WhenGroupMissing_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p10");
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.JoinService.JoinAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_WhenGroupMissing_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p11");
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.ApplicationService.ApplyAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task InviteAsync_WhenInvitationOnlyPolicy_AdminCanInvite()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p12");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: true,
            joinPolicy: GroupJoinPolicy.InvitationOnly);
        scenario.LoginAs(GroupActorRole.Admin);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(await scenario.InvitationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JoinAsync_WhenInvitationOnlyPolicy_ThrowsForStranger()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p13");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.InvitationOnly);
        scenario.LoginAs(GroupActorRole.Admin);
        await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.JoinService.JoinAsync(group.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JoinAsync_ClearsBothInvitationAndApplication()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p14");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.JoinService.JoinAsync(group.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    // --- Join resolution ---

    [Fact]
    public async Task CreateMembershipFromJoinRequests_AddsMemberRole()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_member");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, joinPolicy: GroupJoinPolicy.Open);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
    }

    [Fact]
    public async Task CreateMembershipFromJoinRequests_WhenAlreadyMember_StillClearsJoinArtifacts()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_artifacts");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task CreateMembershipFromJoinRequests_WhenBlacklisted_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_blacklist");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id));
        Assert.Contains("blacklisted", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await scenario.GroupMemberRepository.GetMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task CreateMembershipFromJoinRequests_DeletesInvitationsAndApplications()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_clear");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task DeleteJoinArtifacts_DoesNotAddMembership()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_delete_only");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.DeleteJoinArtifactsForUserAndGroupAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.GroupMemberRepository.GetMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task CreateMembershipFromJoinRequests_DeletesAllInvitationsForUserInGroup()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_invites");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: true);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedInvitationAsync(group.Id, scenario.Admin.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Equal(1, (await scenario.GroupMemberRepository.GetMembersByGroupAsync(group.Id))
            .Count(m => m.UserId == scenario.Stranger.Id));
    }

    [Fact]
    public async Task DeleteJoinArtifacts_RemovesOnlyTargetUserApplications()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr_app_only");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id, isPending: true);
        var ignored = await scenario.SeedApplicationAsync(group.Id, scenario.Member.Id, isPending: false);

        await scenario.JoinResolution.DeleteJoinArtifactsForUserAndGroupAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.NotNull(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Member.Id));
        Assert.Equal(ignored.Id, (await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Member.Id))!.Id);
    }
}
