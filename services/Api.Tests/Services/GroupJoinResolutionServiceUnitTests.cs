using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;

namespace Api.Tests.Services;

public sealed class GroupJoinResolutionServiceUnitTests
{
    [Fact]
    public async Task JR01_CreateMembership_AddsMemberRole()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr01");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, joinPolicy: GroupJoinPolicy.Open);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
    }

    [Fact]
    public async Task JR02_CreateMembership_WhenAlreadyMember_StillClearsJoinArtifacts()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr02");
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
    public async Task JR03_CreateMembership_BlacklistedUser_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr03");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id));
        Assert.Contains("blacklisted", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await scenario.GroupMemberRepository.GetMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JR04_CreateMembership_DeletesInvitationsAndApplications()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr04");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JR05_DeleteJoinArtifacts_DoesNotAddMembership()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr05");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.DeleteJoinArtifactsForUserAndGroupAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.GroupMemberRepository.GetMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task JR06_CreateMembership_DeletesAllInvitationsForUserInGroup()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr06");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: true);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedInvitationAsync(group.Id, scenario.Admin.Id, scenario.Stranger.Id);

        await scenario.JoinResolution.CreateMembershipFromJoinRequestsAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Equal(1, (await scenario.GroupMemberRepository.GetMembersByGroupAsync(group.Id))
            .Count(m => m.UserId == scenario.Stranger.Id));
    }

    [Fact]
    public async Task JR07_CreateMembership_DeletesPendingAndIgnoredApplications()
    {
        var scenario = await GroupTestScenario.CreateAsync("jr07");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id, isPending: true);
        var ignored = await scenario.SeedApplicationAsync(group.Id, scenario.Member.Id, isPending: false);

        await scenario.JoinResolution.DeleteJoinArtifactsForUserAndGroupAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.NotNull(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Member.Id));
        Assert.Equal(ignored.Id, (await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Member.Id))!.Id);
    }
}
