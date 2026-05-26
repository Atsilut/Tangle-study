using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupApplicationServiceUnitTests
{
    // --- Reciprocal join ---

    [Fact]
    public async Task Apply_AfterPendingInvitation_AddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("app_reciprocal");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Stranger.Id);

        var result = await scenario.ApplicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, result.Outcome);
        Assert.Null(result.Application);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        Assert.Null(await scenario.InvitationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
    }

    // --- Appears pending for viewer ---

    [Fact]
    public async Task Apply_ReturnsCreatedWithoutReactivate_WhenResendingAfterOwnerIgnored()
    {
        var scenario = await GroupTestScenario.CreateAsync("app_no_reactivate");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.ApplicationService.IgnoreAsync(application.Id);
        scenario.LoginAs(scenario.Stranger.Id);

        var result = await scenario.ApplicationService.ApplyAsync(group.Id);

        Assert.Equal(GroupApplicationOutcome.GroupApplicationCreated, result.Outcome);
        Assert.NotNull(result.Application);
        Assert.True(result.Application.IsPending);
        Assert.Equal(application.Id, result.Application.Id);
        Assert.False((await scenario.ApplicationRepository.GetByIdAsync(application.Id))!.IsPending);
    }

    [Fact]
    public async Task GetMyApplications_StillShowsOutgoingAsPending_WhenOwnerIgnored()
    {
        var scenario = await GroupTestScenario.CreateAsync("app_pending_view");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.ApplicationService.IgnoreAsync(application.Id);
        scenario.LoginAs(scenario.Stranger.Id);

        var list = await scenario.ApplicationService.GetMyApplicationsAsync();

        var only = Assert.Single(list!);
        Assert.Equal(application.Id, only.Id);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    // --- Blacklist interaction ---

    [Fact]
    public async Task Approve_ThrowsNotFound_WhenApplicationRemovedByBlacklist()
    {
        var scenario = await GroupTestScenario.CreateAsync("app_blacklist_approve");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.ApplicationService.ApproveAsync(application.Id));
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Stranger.Id);
    }
}
