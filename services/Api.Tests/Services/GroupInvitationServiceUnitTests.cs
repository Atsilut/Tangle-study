using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupInvitationServiceUnitTests
{
    // --- Reciprocal join ---

    [Fact]
    public async Task Invite_AfterPendingApplication_AddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_reciprocal");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication, result.Outcome);
        Assert.Null(result.Invitation);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        Assert.Null(await scenario.ApplicationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutJoin_WhenReciprocalApplicationAndBlockExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_recip_block");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.ApplicationService.ApplyAsync(group.Id);
        await scenario.UserBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = scenario.Owner.Id });
        scenario.LoginAs(GroupActorRole.Owner);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Stranger.Id);
    }

    // --- Storage ---

    [Fact]
    public async Task Invite_StoresAtMostOneRowPerGroupInvitee()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_one_row");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });
        await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        var pending = await scenario.InvitationRepository.GetPendingIncomingForInviteeAsync(scenario.Stranger.Id);
        Assert.Single(pending);
    }

    [Fact]
    public async Task Reject_AllowsReinvite()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_reinvite");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.InvitationService.RejectAsync(invitation.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
    }

    [Fact]
    public async Task Cancel_DeletesInvitation_WhenCalledByInviter()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_cancel");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);

        await scenario.InvitationService.CancelAsync(invitation.Id);

        Assert.Null(await scenario.InvitationRepository.GetByIdAsync(invitation.Id));
    }

    // --- Appears pending for viewer ---

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutReactivate_WhenResendingAfterInviteeIgnored()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_no_reactivate");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.InvitationService.IgnoreAsync(invitation.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        Assert.NotNull(result.Invitation);
        Assert.True(result.Invitation.IsPending);
        Assert.Equal(invitation.Id, result.Invitation.Id);
        Assert.False((await scenario.InvitationRepository.GetByIdAsync(invitation.Id))!.IsPending);
    }

    [Fact]
    public async Task GetMyPending_StillShowsOutgoingAsPending_WhenInviteeIgnored()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_pending_view");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.InvitationService.IgnoreAsync(invitation.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var list = await scenario.InvitationService.GetMyPendingAsync();

        var only = Assert.Single(list!);
        Assert.Equal(invitation.Id, only.Id);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    // --- User blocks ---

    [Fact]
    public async Task Invite_ThrowsArgument_WhenInviterBlockedInvitee()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_block_out");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        scenario.LoginAs(scenario.Owner.Id);
        await scenario.UserBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = scenario.Stranger.Id });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }));
    }

    [Fact]
    public async Task Invite_CreatesNonPendingInvitation_WhenInviteeBlockedInviter()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_block_in");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.UserBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = scenario.Owner.Id });
        scenario.LoginAs(GroupActorRole.Owner);

        var result = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
        var stored = await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsPending);
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenInviteeBlockedInviter()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_accept_block_in");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Stranger.Id);
        await scenario.UserBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = scenario.Owner.Id });
        await scenario.InvitationService.IgnoreAsync(invitation.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.InvitationService.AcceptAsync(invitation.Id));
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Stranger.Id);
    }

    [Fact]
    public async Task Accept_ThrowsArgument_WhenInviterBlockedInvitee()
    {
        var scenario = await GroupTestScenario.CreateAsync("inv_accept_block_out");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(scenario.Owner.Id);
        await scenario.UserBlockService.BlockUserAsync(
            new UserBlockCreateRequestDto { BlockedUserId = scenario.Stranger.Id });
        scenario.LoginAs(scenario.Stranger.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.InvitationService.AcceptAsync(invitation.Id));
        Assert.NotNull(await scenario.InvitationRepository.GetByIdAsync(invitation.Id));
        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Stranger.Id);
    }
}
