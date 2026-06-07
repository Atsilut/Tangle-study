using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupInvitationApplicationIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
    public static TheoryData<GroupActorRole, GroupExpectedOutcome> InviteAuthorizationData =>
        new()
        {
            { GroupActorRole.Owner, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(InviteAuthorizationData))]
    public async Task InviteAuthorization_Matrix(GroupActorRole caller, GroupExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"invite_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupInvitationOnlyGroupAsync(
            includeAdmin: true,
            includeMember: caller == GroupActorRole.Member);
        await scenario.LoginAsAsync(caller);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }, TestContext.Current.CancellationToken);

        // Assert
        if (expected == GroupExpectedOutcome.Ok) await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<GroupActorRole, InvitationRequestAction, GroupExpectedOutcome> InvitationActionData =>
        new()
        {
            { GroupActorRole.Stranger, InvitationRequestAction.Accept, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, InvitationRequestAction.AcceptAsNonInvitee, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Admin, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, InvitationRequestAction.Accept, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, InvitationRequestAction.Reject, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, InvitationRequestAction.Reject, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, InvitationRequestAction.Ignore, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, InvitationRequestAction.Ignore, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, InvitationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, InvitationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, InvitationRequestAction.CancelAsNonInviterAdmin, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, InvitationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, InvitationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(InvitationActionData))]
    public async Task InvitationAction_Matrix(
        GroupActorRole caller,
        InvitationRequestAction action,
        GroupExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"inv_act_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupInvitationOnlyGroupAsync(
            includeAdmin: true,
            includeMember: caller == GroupActorRole.Member);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(caller);

        // Act
        HttpResponseMessage res = action switch
        {
            InvitationRequestAction.Accept or InvitationRequestAction.AcceptAsNonInvitee =>
                await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", null, TestContext.Current.CancellationToken),
            InvitationRequestAction.Reject => await Client.PostAsync($"/api/invitations/{invitation.Id}/reject", null, TestContext.Current.CancellationToken),
            InvitationRequestAction.Ignore => await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken),
            InvitationRequestAction.Cancel or InvitationRequestAction.CancelAsNonInviterAdmin =>
                await Client.DeleteAsync($"/api/invitations/{invitation.Id}", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        // Assert
        if (expected == GroupExpectedOutcome.Ok)
        {
            Assert.True(
                res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
                res.StatusCode.ToString());
            if (action is InvitationRequestAction.Accept or InvitationRequestAction.AcceptAsNonInvitee)
                await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
        }
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<GroupActorRole, ApplicationRequestAction, GroupExpectedOutcome> ApplicationActionData =>
        new()
        {
            { GroupActorRole.Owner, ApplicationRequestAction.Approve, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, ApplicationRequestAction.Approve, GroupExpectedOutcome.Ok },
            { GroupActorRole.Stranger, ApplicationRequestAction.ApproveAsApplicant, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, ApplicationRequestAction.Approve, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, ApplicationRequestAction.Approve, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, ApplicationRequestAction.Reject, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, ApplicationRequestAction.Reject, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, ApplicationRequestAction.Ignore, GroupExpectedOutcome.Ok },
            { GroupActorRole.Stranger, ApplicationRequestAction.Ignore, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, ApplicationRequestAction.Cancel, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, ApplicationRequestAction.Cancel, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(ApplicationActionData))]
    public async Task ApplicationAction_Matrix(
        GroupActorRole caller,
        ApplicationRequestAction action,
        GroupExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync($"app_act_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupRequestableGroupAsync(includeAdmin: true);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, scenario.Member.Id, GroupRole.Member);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(caller);

        // Act
        HttpResponseMessage res = action switch
        {
            ApplicationRequestAction.Approve or ApplicationRequestAction.ApproveAsApplicant => await Client.PostAsync(
                $"/api/applications/{application.Id}/approve", null, TestContext.Current.CancellationToken),
            ApplicationRequestAction.Reject => await Client.PostAsync(
                $"/api/applications/{application.Id}/reject", null, TestContext.Current.CancellationToken),
            ApplicationRequestAction.Ignore => await Client.PostAsync(
                $"/api/applications/{application.Id}/ignore", null, TestContext.Current.CancellationToken),
            ApplicationRequestAction.Cancel => await Client.DeleteAsync($"/api/applications/{application.Id}", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        // Assert
        if (expected == GroupExpectedOutcome.Ok)
        {
            Assert.True(
                res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
                res.StatusCode.ToString());
            if (action == ApplicationRequestAction.Approve)
                await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
        }
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    [Fact]
    public async Task GetPendingApplications_OwnerSeesApplicant()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("gal01");
        var group = await scenario.SetupRequestableGroupAsync();
        await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>(TestContext.Current.CancellationToken);
        Assert.Single(list!);
        Assert.Equal(scenario.Stranger.Id, list[0].ApplicantId);
    }

    [Fact]
    public async Task GetMyApplications_ApplicantSeesOutgoing()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("gal04");
        var group = await scenario.SetupRequestableGroupAsync();
        await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.GetAsync("/api/applications/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent);
        if (res.StatusCode == HttpStatusCode.OK)
        {
            var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>(TestContext.Current.CancellationToken);
            Assert.Contains(list!, dto => dto.GroupId == group.Id && !dto.IsIncoming);
        }
    }

    // --- Reciprocal join ---

    [Fact]
    public async Task InviteAfterApply_Returns200_AndAddsMember()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_recip");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var invite = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(invite, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);

        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        var mine = await Client.GetAsync("/api/invitations/me", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(mine, HttpStatusCode.NoContent);
    }

    // --- Invitation queries and sequences ---

    [Fact]
    public async Task GetMyInvitations_ListsPending()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_me");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.GetAsync("/api/invitations/me", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>(TestContext.Current.CancellationToken);
        var only = Assert.Single(list!);
        Assert.Equal(group.Id, only.GroupId);
    }

    [Fact]
    public async Task IgnoreThenAccept_AddsMember()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_ignore_accept");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var ignore = await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken);
        var accept = await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(ignore, HttpStatusCode.NoContent);
        await IntegrationAssertions.AssertStatusAsync(accept, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task Invite_ReturnsCreatedWithoutReactivate_WhenResendingAfterInviteeIgnored()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_resend");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var resend = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(resend, HttpStatusCode.Created);
        var dto = await resend.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>(TestContext.Current.CancellationToken);
        Assert.True(dto!.IsPending);
        Assert.Equal(invitation.Id, dto.Id);
    }

    [Fact]
    public async Task GetIgnoredIncoming_Returns204_WhenEmpty()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_ignored_empty");
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.GetAsync("/api/invitations/ignored", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetIgnoredIncoming_ListsIgnored_ForInvitee()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_ignored_list");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken);

        // Act
        var res = await Client.GetAsync("/api/invitations/ignored", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>(TestContext.Current.CancellationToken);
        var only = Assert.Single(list!);
        Assert.Equal(invitation.Id, only.Id);
        Assert.False(only.IsPending);
        Assert.True(only.IsIncoming);
    }

    [Fact]
    public async Task GetMyInvitations_ShowsMaskedPending_ForInviterAfterInviteeIgnored()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_masked");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.GetAsync("/api/invitations/me", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupInvitationCreateResponseDto>>(TestContext.Current.CancellationToken);
        var only = Assert.Single(list!);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task Invite_Returns400_WhenInviterBlockedInvitee()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_block_out");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        await scenario.LoginAsAsync(GroupActorRole.Owner);
        await GroupIntegrationTestHelpers.BlockUserAsync(Client, scenario.Stranger.Id);

        // Act
        var invite = await Client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(invite, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Accept_Returns400_WhenInviteeBlockedInviter()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("inv_block_in");
        var group = await scenario.SetupInvitationOnlyGroupAsync(includeMember: false);
        var invitation = await scenario.InviteStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);
        await GroupIntegrationTestHelpers.BlockUserAsync(Client, scenario.Owner.Id);
        await Client.PostAsync($"/api/invitations/{invitation.Id}/ignore", null, TestContext.Current.CancellationToken);

        // Act
        var accept = await Client.PostAsync($"/api/invitations/{invitation.Id}/accept", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(accept, HttpStatusCode.BadRequest);
    }

    // --- Application viewer semantics ---

    [Fact]
    public async Task Ignore_Returns204_AndApproveStillWorks()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("app_ignore_approve");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var ignore = await Client.PostAsync($"/api/applications/{application.Id}/ignore", null, TestContext.Current.CancellationToken);
        var approve = await Client.PostAsync($"/api/applications/{application.Id}/approve", null, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(ignore, HttpStatusCode.NoContent);
        await IntegrationAssertions.AssertStatusAsync(approve, HttpStatusCode.OK);
        await scenario.AssertIsMemberAsync(group.Id, scenario.Stranger.Id, true);
    }

    [Fact]
    public async Task GetMyApplications_ShowsMaskedPending_AfterAdminIgnored()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("app_masked");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Owner);
        await Client.PostAsync($"/api/applications/{application.Id}/ignore", null, TestContext.Current.CancellationToken);
        await scenario.LoginAsAsync(GroupActorRole.Stranger);

        // Act
        var res = await Client.GetAsync("/api/applications/me", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>(TestContext.Current.CancellationToken);
        var only = Assert.Single(list!);
        Assert.True(only.IsPending);
        Assert.False(only.IsIncoming);
    }

    [Fact]
    public async Task GetIgnoredApplications_Returns204_WhenEmpty()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("app_ignored_empty");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.GetAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications/ignored", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetIgnoredApplications_ListsIgnored_ForAdmin()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("app_ignored_list");
        var group = await scenario.SetupRequestableGroupAsync(includeMember: false);
        var application = await scenario.ApplyAsStrangerAsync(group.Id);
        await scenario.LoginAsAsync(GroupActorRole.Owner);
        await Client.PostAsync($"/api/applications/{application.Id}/ignore", null, TestContext.Current.CancellationToken);

        // Act
        var res = await Client.GetAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/applications/ignored", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var list = await res.Content.ReadFromJsonAsync<List<GroupApplicationResponseDto>>(TestContext.Current.CancellationToken);
        var only = Assert.Single(list!);
        Assert.Equal(application.Id, only.Id);
        Assert.False(only.IsPending);
        Assert.True(only.IsIncoming);
    }
}
