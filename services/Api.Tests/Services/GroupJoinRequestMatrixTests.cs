using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupJoinRequestMatrixTests
{
    #region Invite authorization (IV-A)

    public static TheoryData<string, GroupActorRole, GroupExpectedOutcome> InviteAuthorizationData =>
        new()
        {
            { "IV-A-01", GroupActorRole.Owner, GroupExpectedOutcome.Ok },
            { "IV-A-02", GroupActorRole.Admin, GroupExpectedOutcome.Ok },
            { "IV-A-03", GroupActorRole.Member, GroupExpectedOutcome.Unauthorized },
            { "IV-A-04", GroupActorRole.Stranger, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(InviteAuthorizationData))]
    public async Task InviteAuthorization_Matrix(
        string caseId,
        GroupActorRole caller,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"iva_{caseId}");
        var group = await scenario.SetupInvitationOnlyGroupAsync(
            includeAdmin: true,
            includeMember: caller == GroupActorRole.Member);
        scenario.LoginAs(caller);

        if (expected == GroupExpectedOutcome.Ok)
        {
            var result = await scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });
            Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, result.Outcome);
            Assert.NotNull(result.Invitation);
            Assert.True(result.Invitation!.IsPending);
            Assert.NotNull(await scenario.InvitationRepository.GetPendingForUserAsync(group.Id, scenario.Stranger.Id));
            Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.InvitationService.InviteAsync(
                    group.Id,
                    new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }));
            Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        }
    }

    #endregion

    #region Invite preconditions (IV-P)

    [Fact]
    public async Task IV_P01_InviteSelf_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivp01");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Owner.Id }));
        Assert.Contains("yourself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IV_P02_InviteExistingMember_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivp02");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Member.Id, GroupRole.Member);
        scenario.LoginAs(GroupActorRole.Owner);

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Member.Id }));
    }

    [Fact]
    public async Task IV_P03_InviteBlacklistedUser_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivp03");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task IV_P05_SameInviterReinvite_ReturnsExisting()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivp05");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var second = await scenario.InvitationService.InviteAsync(
            group.Id,
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id });

        Assert.Equal(GroupInvitationOutcome.GroupInvitationCreated, second.Outcome);
        Assert.Single(await scenario.InvitationRepository.GetPendingIncomingForInviteeAsync(scenario.Stranger.Id));
    }

    #endregion

    #region Invitation actions (IV-C)

    public static TheoryData<string, GroupActorRole, InvitationRequestAction, GroupExpectedOutcome> InvitationActionData =>
        new()
        {
            { "IV-C-01", GroupActorRole.Stranger, InvitationRequestAction.Accept, GroupExpectedOutcome.Ok },
            { "IV-C-02", GroupActorRole.Owner, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { "IV-C-03", GroupActorRole.Admin, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { "IV-C-04", GroupActorRole.Member, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { "IV-C-05", GroupActorRole.Member, InvitationRequestAction.AcceptAsNonInvitee, GroupExpectedOutcome.Unauthorized },
            { "IV-C-06", GroupActorRole.Stranger, InvitationRequestAction.Reject, GroupExpectedOutcome.Ok },
            { "IV-C-07", GroupActorRole.Owner, InvitationRequestAction.Reject, GroupExpectedOutcome.Unauthorized },
            { "IV-C-08", GroupActorRole.Stranger, InvitationRequestAction.Ignore, GroupExpectedOutcome.Ok },
            { "IV-C-09", GroupActorRole.Owner, InvitationRequestAction.Ignore, GroupExpectedOutcome.Unauthorized },
            { "IV-C-11", GroupActorRole.Owner, InvitationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { "IV-C-12", GroupActorRole.Admin, InvitationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { "IV-C-13", GroupActorRole.Owner, InvitationRequestAction.CancelAsNonInviterAdmin, GroupExpectedOutcome.Ok },
            { "IV-C-14", GroupActorRole.Member, InvitationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
            { "IV-C-15", GroupActorRole.Stranger, InvitationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(InvitationActionData))]
    public async Task InvitationAction_Matrix(
        string caseId,
        GroupActorRole caller,
        InvitationRequestAction action,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"ivc_{caseId}");
        var group = await scenario.SetupInvitationOnlyGroupAsync(
            includeAdmin: true,
            includeMember: caller == GroupActorRole.Member
                || action == InvitationRequestAction.AcceptAsNonInvitee);
        GroupInvitation invitation;
        if (action == InvitationRequestAction.CancelAsNonInviterAdmin)
            invitation = await scenario.SeedInvitationAsync(group.Id, scenario.Admin.Id, scenario.Stranger.Id);
        else
            invitation = await scenario.InviteStrangerAsync(group.Id);

        scenario.LoginAs(caller);

        switch (action)
        {
            case InvitationRequestAction.Accept:
            case InvitationRequestAction.AcceptAsNonInvitee:
                await RunInvitationAcceptAsync(scenario, group.Id, invitation.Id, caller, expected);
                break;
            case InvitationRequestAction.Reject:
                await RunInvitationRejectAsync(scenario, group.Id, invitation.Id, expected);
                break;
            case InvitationRequestAction.Ignore:
                await RunInvitationIgnoreAsync(scenario, invitation.Id, caller, expected);
                break;
            case InvitationRequestAction.Cancel:
            case InvitationRequestAction.CancelAsNonInviterAdmin:
                await RunInvitationCancelAsync(scenario, invitation, caller, action, expected);
                break;
        }
    }

    [Fact]
    public async Task IV_C10_IgnoreTwice_IsNoOp()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivc10");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Stranger);
        await scenario.InvitationService.IgnoreAsync(invitation.Id);

        await scenario.InvitationService.IgnoreAsync(invitation.Id);

        var stored = await scenario.InvitationRepository.GetByIdAsync(invitation.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsPending);
    }

    [Fact]
    public async Task IV_C16_CancelNonPending_ThrowsInvalid()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivc16");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Stranger);
        await scenario.InvitationService.IgnoreAsync(invitation.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.InvitationService.CancelAsync(invitation.Id));
        Assert.NotNull(await scenario.InvitationRepository.GetByIdAsync(invitation.Id));
    }

    [Fact]
    public async Task IV_C18_AcceptWhenAlreadyMember_IsIdempotent()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivc18");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.InvitationService.AcceptAsync(invitation.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task IV_C19_AcceptIgnoredIncoming_AddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivc19");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        var invitation = await scenario.SeedInvitationAsync(
            group.Id, scenario.Owner.Id, scenario.Stranger.Id, isPending: false);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.InvitationService.AcceptAsync(invitation.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    #endregion

    #region Application actions (GA-C)

    public static TheoryData<string, GroupActorRole, ApplicationRequestAction, GroupExpectedOutcome> ApplicationActionData =>
        new()
        {
            { "GA-C-01", GroupActorRole.Owner, ApplicationRequestAction.Approve, GroupExpectedOutcome.Ok },
            { "GA-C-02", GroupActorRole.Admin, ApplicationRequestAction.Approve, GroupExpectedOutcome.Ok },
            { "GA-C-03", GroupActorRole.Member, ApplicationRequestAction.Approve, GroupExpectedOutcome.Unauthorized },
            { "GA-C-04", GroupActorRole.Stranger, ApplicationRequestAction.Approve, GroupExpectedOutcome.Unauthorized },
            { "GA-C-05", GroupActorRole.Stranger, ApplicationRequestAction.ApproveAsApplicant, GroupExpectedOutcome.Unauthorized },
            { "GA-C-06", GroupActorRole.Owner, ApplicationRequestAction.Reject, GroupExpectedOutcome.Ok },
            { "GA-C-07", GroupActorRole.Member, ApplicationRequestAction.Reject, GroupExpectedOutcome.Unauthorized },
            { "GA-C-08", GroupActorRole.Owner, ApplicationRequestAction.Ignore, GroupExpectedOutcome.Ok },
            { "GA-C-09", GroupActorRole.Stranger, ApplicationRequestAction.Ignore, GroupExpectedOutcome.Unauthorized },
            { "GA-C-10", GroupActorRole.Stranger, ApplicationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { "GA-C-11", GroupActorRole.Owner, ApplicationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(ApplicationActionData))]
    public async Task ApplicationAction_Matrix(
        string caseId,
        GroupActorRole caller,
        ApplicationRequestAction action,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"gac_{caseId}");
        var group = await scenario.SetupRequestableGroupAsync(includeAdmin: true);
        if (action != ApplicationRequestAction.ApproveAsApplicant)
            await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Member.Id, GroupRole.Member);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(caller);

        switch (action)
        {
            case ApplicationRequestAction.Approve:
            case ApplicationRequestAction.ApproveAsApplicant:
                await RunApplicationApproveAsync(scenario, group.Id, application.Id, expected);
                break;
            case ApplicationRequestAction.Reject:
                await RunApplicationRejectAsync(scenario, group.Id, application.Id, expected);
                break;
            case ApplicationRequestAction.Ignore:
                await RunApplicationIgnoreAsync(scenario, application.Id, expected);
                break;
            case ApplicationRequestAction.Cancel:
                await RunApplicationCancelAsync(scenario, application.Id, expected);
                break;
        }
    }

    [Fact]
    public async Task GA_C12_CancelNonPending_ThrowsInvalid()
    {
        var scenario = await GroupTestScenario.CreateAsync("gac12");
        var group = await scenario.SetupRequestableGroupAsync();
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.ApplicationService.IgnoreAsync(application.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.ApplicationService.CancelAsync(application.Id));
    }

    [Fact]
    public async Task GA_C13_ApproveWhenAlreadyMember_IsIdempotent()
    {
        var scenario = await GroupTestScenario.CreateAsync("gac13");
        var group = await scenario.SetupRequestableGroupAsync();
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.ApplicationService.ApproveAsync(application.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task GA_C14_ApproveIgnoredApplication_AddsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("gac14");
        var group = await scenario.SetupRequestableGroupAsync();
        var application = await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id, isPending: false);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.ApplicationService.ApproveAsync(application.Id);

        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    #endregion

    #region List / query (IV-L, GA-L)

    [Fact]
    public async Task IV_L01_GetMyPending_IncludesIncomingForInvitee()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivl01");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        var list = await scenario.InvitationService.GetMyPendingAsync();

        Assert.NotNull(list);
        Assert.Contains(list!, dto => dto.GroupId == group.Id && dto.IsIncoming);
    }

    [Fact]
    public async Task IV_L02_GetMyPending_NullWhenNoInvites()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivl02");
        scenario.LoginAs(GroupActorRole.Stranger);

        Assert.Null(await scenario.InvitationService.GetMyPendingAsync());
    }

    [Fact]
    public async Task GA_L01_GetPendingByGroup_OwnerSeesApplications()
    {
        var scenario = await GroupTestScenario.CreateAsync("gal01");
        var group = await scenario.SetupRequestableGroupAsync();
        await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var list = await scenario.ApplicationService.GetPendingByGroupAsync(group.Id);

        Assert.Single(list);
        Assert.Equal(scenario.Stranger.Id, list[0].ApplicantId);
    }

    [Fact]
    public async Task GA_L02_GetPendingByGroup_MemberUnauthorized()
    {
        var scenario = await GroupTestScenario.CreateAsync("gal02");
        var group = await scenario.SetupRequestableGroupAsync();
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Member.Id, GroupRole.Member);
        await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Member);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.ApplicationService.GetPendingByGroupAsync(group.Id));
    }

    [Fact]
    public async Task GA_L03_GetPendingByGroup_StrangerUnauthorized()
    {
        var scenario = await GroupTestScenario.CreateAsync("gal03");
        var group = await scenario.SetupRequestableGroupAsync();
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.ApplicationService.GetPendingByGroupAsync(group.Id));
    }

    [Fact]
    public async Task GA_L04_GetMyApplications_ApplicantSeesOutgoing()
    {
        var scenario = await GroupTestScenario.CreateAsync("gal04");
        var group = await scenario.SetupRequestableGroupAsync();
        await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        var list = await scenario.ApplicationService.GetMyApplicationsAsync();

        Assert.NotNull(list);
        Assert.Contains(list!, dto => dto.GroupId == group.Id && !dto.IsIncoming);
    }

    [Fact]
    public async Task GA_L05_GetIgnoredByGroup_AdminAfterIgnore()
    {
        var scenario = await GroupTestScenario.CreateAsync("gal05");
        var group = await scenario.SetupRequestableGroupAsync(includeAdmin: true);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Admin);
        await scenario.ApplicationService.IgnoreAsync(application.Id);

        var list = await scenario.ApplicationService.GetIgnoredByGroupAsync(group.Id);

        Assert.NotNull(list);
        Assert.Contains(list!, dto => dto.Id == application.Id);
    }

    [Fact]
    public async Task IV_L06_GetIgnoredIncoming_InviteeAfterIgnore()
    {
        var scenario = await GroupTestScenario.CreateAsync("ivl06");
        var group = await scenario.SetupInvitationOnlyGroupAsync();
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        scenario.LoginAs(GroupActorRole.Stranger);
        await scenario.InvitationService.IgnoreAsync(invitation.Id);

        var list = await scenario.InvitationService.GetIgnoredIncomingAsync();

        Assert.NotNull(list);
        Assert.Contains(list!, dto => dto.Id == invitation.Id);
    }

    #endregion

    private static async Task RunInvitationAcceptAsync(
        GroupTestScenario scenario,
        long groupId,
        long invitationId,
        GroupActorRole caller,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.InvitationService.AcceptAsync(invitationId);
            Assert.True(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.Null(await scenario.InvitationRepository.GetByIdAsync(invitationId));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.InvitationService.AcceptAsync(invitationId));
            Assert.False(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.NotNull(await scenario.InvitationRepository.GetByIdAsync(invitationId));
        }
    }

    private static async Task RunInvitationRejectAsync(
        GroupTestScenario scenario,
        long groupId,
        long invitationId,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.InvitationService.RejectAsync(invitationId);
            Assert.False(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.Null(await scenario.InvitationRepository.GetByIdAsync(invitationId));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.InvitationService.RejectAsync(invitationId));
            Assert.NotNull(await scenario.InvitationRepository.GetByIdAsync(invitationId));
        }
    }

    private static async Task RunInvitationIgnoreAsync(
        GroupTestScenario scenario,
        long invitationId,
        GroupActorRole caller,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.InvitationService.IgnoreAsync(invitationId);
            var stored = await scenario.InvitationRepository.GetByIdAsync(invitationId);
            Assert.NotNull(stored);
            Assert.False(stored!.IsPending);
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.InvitationService.IgnoreAsync(invitationId));
        }
    }

    private static async Task RunInvitationCancelAsync(
        GroupTestScenario scenario,
        GroupInvitation invitation,
        GroupActorRole caller,
        InvitationRequestAction action,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.InvitationService.CancelAsync(invitation.Id);
            Assert.Null(await scenario.InvitationRepository.GetByIdAsync(invitation.Id));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.InvitationService.CancelAsync(invitation.Id));
            Assert.NotNull(await scenario.InvitationRepository.GetByIdAsync(invitation.Id));
        }
    }

    private static async Task RunApplicationApproveAsync(
        GroupTestScenario scenario,
        long groupId,
        long applicationId,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.ApplicationService.ApproveAsync(applicationId);
            Assert.True(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.Null(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.ApplicationService.ApproveAsync(applicationId));
            Assert.False(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.NotNull(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
    }

    private static async Task RunApplicationRejectAsync(
        GroupTestScenario scenario,
        long groupId,
        long applicationId,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.ApplicationService.RejectAsync(applicationId);
            Assert.False(await scenario.MembershipService.IsMemberAsync(groupId, scenario.Stranger.Id));
            Assert.Null(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.ApplicationService.RejectAsync(applicationId));
            Assert.NotNull(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
    }

    private static async Task RunApplicationIgnoreAsync(
        GroupTestScenario scenario,
        long applicationId,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.ApplicationService.IgnoreAsync(applicationId);
            var stored = await scenario.ApplicationRepository.GetByIdAsync(applicationId);
            Assert.NotNull(stored);
            Assert.False(stored!.IsPending);
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.ApplicationService.IgnoreAsync(applicationId));
        }
    }

    private static async Task RunApplicationCancelAsync(
        GroupTestScenario scenario,
        long applicationId,
        GroupExpectedOutcome expected)
    {
        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.ApplicationService.CancelAsync(applicationId);
            Assert.Null(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
        else
        {
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.ApplicationService.CancelAsync(applicationId));
            Assert.Contains("applicant", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(await scenario.ApplicationRepository.GetByIdAsync(applicationId));
        }
    }
}

public enum InvitationRequestAction
{
    Accept,
    AcceptAsNonInvitee,
    Reject,
    Ignore,
    Cancel,
    CancelAsNonInviterAdmin,
}

public enum ApplicationRequestAction
{
    Approve,
    ApproveAsApplicant,
    Reject,
    Ignore,
    Cancel,
}
