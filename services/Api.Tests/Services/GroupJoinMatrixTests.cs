using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupJoinMatrixTests
{
    public static TheoryData<string, GroupJoinPolicy, JoinPolicyOperation, JoinPolicyRouteOutcome> PolicyRoutingMatrixData =>
        new()
        {
            { "JP-R-01", GroupJoinPolicy.Open, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.MemberAdded },
            { "JP-R-02", GroupJoinPolicy.Open, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.UseJoinEndpoint },
            { "JP-R-03", GroupJoinPolicy.Requestable, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.RequiresApplication },
            { "JP-R-04", GroupJoinPolicy.Requestable, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.ApplicationCreated },
            { "JP-R-05", GroupJoinPolicy.InvitationOnly, JoinPolicyOperation.Join, JoinPolicyRouteOutcome.InvitationOnly },
            { "JP-R-06", GroupJoinPolicy.InvitationOnly, JoinPolicyOperation.Apply, JoinPolicyRouteOutcome.InvitationOnly },
        };

    [Theory]
    [MemberData(nameof(PolicyRoutingMatrixData))]
    public async Task PolicyRouting_Matrix(
        string caseId,
        GroupJoinPolicy joinPolicy,
        JoinPolicyOperation operation,
        JoinPolicyRouteOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"jp_r_{caseId}");
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

    [Fact]
    public async Task JP_R00_CreateGroup_PersistsJoinPolicy()
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
    public async Task JP_P01_Join_WhenAlreadyMember_Throws()
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
    public async Task JP_P02_Apply_WhenAlreadyMember_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p02");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        scenario.LoginAs(GroupActorRole.Member);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() => scenario.ApplicationService.ApplyAsync(group.Id));
    }

    [Fact]
    public async Task JP_P03_BlacklistedUser_JoinOpen_Throws()
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
    public async Task JP_P04_BlacklistedUser_ApplyRequestable_Throws()
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
    public async Task JP_P05_OpenJoin_WithPendingInvitation_ClearsArtifacts()
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
    public async Task JP_P06_OpenJoin_WithPendingApplication_ClearsArtifacts()
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
    public async Task JP_P07_OpenJoin_WithIgnoredInvitation_ClearsRowAndAddsMember()
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
    public async Task JP_P08_Apply_WithPendingInvitation_ReciprocalJoin()
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
    public async Task JP_P09_Apply_DuplicatePending_ReturnsCreatedWithoutSecondRow()
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
    public async Task JP_P10_Join_MissingGroup_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p10");
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.JoinService.JoinAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task JP_P11_Apply_MissingGroup_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("jp_p11");
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.ApplicationService.ApplyAsync(99999));
        Assert.Equal("Group not found", ex.Message);
    }

    [Fact]
    public async Task JP_P12_InvitationOnly_AdminCanStillInvite()
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
    public async Task JP_P13_InvitationOnly_StrangerJoinStillBlocked()
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
    public async Task JP_P14_OpenJoin_ClearsBothInvitationAndApplication()
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
}

public enum JoinPolicyOperation
{
    Join,
    Apply,
}

public enum JoinPolicyRouteOutcome
{
    MemberAdded,
    UseJoinEndpoint,
    RequiresApplication,
    InvitationOnly,
    ApplicationCreated,
}
