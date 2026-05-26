using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupBlacklistJoinMatrixTests
{
    #region Blacklist × join path (BL-J)

    [Fact]
    public async Task BL_J01_BlacklistedUser_OpenJoin_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj01");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.JoinService.JoinAsync(group.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J02_BlacklistedUser_ApplyRequestable_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj02");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.ApplicationService.ApplyAsync(group.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J03_InviteBlacklistedUser_Throws_NoInvitationRow()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj03");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: true,
            joinPolicy: GroupJoinPolicy.InvitationOnly);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.InvitationService.InviteAsync(
                group.Id,
                new GroupInvitationCreateRequestDto { InviteeId = scenario.Stranger.Id }));
        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J04_AcceptAfterBlacklist_InvitationRemoved_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj04");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.InvitationOnly);
        var invitation = await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.InvitationService.AcceptAsync(invitation.Id));
        Assert.Equal("Invitation not found", ex.Message);
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J05_ApproveAfterBlacklist_ApplicationRemoved_ThrowsNotFound()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj05");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Requestable);
        var application = await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            scenario.ApplicationService.ApproveAsync(application.Id));
        Assert.Equal("Application not found", ex.Message);
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J06_Blacklist_KicksExistingMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj06");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        scenario.LoginAs(GroupActorRole.Stranger);
        await scenario.JoinService.JoinAsync(group.Id);
        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));

        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J07_EnsureNotBlacklisted_ThrowsWhenListed()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj07");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.BlacklistService.EnsureNotBlacklistedAsync(group.Id, scenario.Stranger.Id));
        Assert.Contains("blacklisted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BL_J08_RemoveFromBlacklist_ThenOpenJoin_Succeeds()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj08");
        var group = await scenario.SetupGroupAsync(
            GroupVisibility.Public,
            joinPolicy: GroupJoinPolicy.Open);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.BlacklistService.RemoveAsync(group.Id, scenario.Stranger.Id);
        scenario.LoginAs(GroupActorRole.Stranger);

        await scenario.JoinService.JoinAsync(group.Id);

        Assert.False(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
        Assert.True(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_J09_AddBlacklist_ClearsPendingInvitationAndApplication()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj09");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        await scenario.SeedInvitationAsync(group.Id, scenario.Owner.Id, scenario.Stranger.Id);
        await scenario.SeedApplicationAsync(group.Id, scenario.Stranger.Id);
        await scenario.MembershipService.AddMemberInternalAsync(group.Id, scenario.Stranger.Id, GroupRole.Member);

        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        Assert.Null(await scenario.InvitationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
        Assert.Null(await scenario.ApplicationRepository.GetForUserAsync(group.Id, scenario.Stranger.Id));
    }

    #endregion

    #region Blacklist admin (BL-A)

    public static TheoryData<string, GroupActorRole, BlacklistAdminAction, GroupExpectedOutcome> AdminAuthorizationData =>
        new()
        {
            { "BL-A-01", GroupActorRole.Owner, BlacklistAdminAction.Add, GroupExpectedOutcome.Ok },
            { "BL-A-02", GroupActorRole.Admin, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { "BL-A-03", GroupActorRole.Member, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { "BL-A-07", GroupActorRole.Admin, BlacklistAdminAction.Remove, GroupExpectedOutcome.Unauthorized },
            { "BL-A-08", GroupActorRole.Owner, BlacklistAdminAction.Remove, GroupExpectedOutcome.Ok },
        };

    [Theory]
    [MemberData(nameof(AdminAuthorizationData))]
    public async Task AdminAuthorization_Matrix(
        string caseId,
        GroupActorRole caller,
        BlacklistAdminAction action,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"bla_{caseId}");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(caller);

        if (action == BlacklistAdminAction.Add)
        {
            if (expected == GroupExpectedOutcome.Ok)
            {
                var dto = await scenario.BlacklistService.AddAsync(
                    group.Id,
                    new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id });
                Assert.Equal(scenario.Stranger.Id, dto.UserId);
                Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
            }
            else
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.BlacklistService.AddAsync(
                        group.Id,
                        new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id }));
                Assert.False(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
            }

            return;
        }

        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.BlacklistService.AddAsync(
            group.Id,
            new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id });
        scenario.LoginAs(caller);

        if (expected == GroupExpectedOutcome.Ok)
        {
            await scenario.BlacklistService.RemoveAsync(group.Id, scenario.Stranger.Id);
            Assert.False(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scenario.BlacklistService.RemoveAsync(group.Id, scenario.Stranger.Id));
            Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
        }
    }

    [Fact]
    public async Task BL_A04_AddSelf_ThrowsArgument()
    {
        var scenario = await GroupTestScenario.CreateAsync("bla04");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        scenario.LoginAs(GroupActorRole.Owner);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.BlacklistService.AddAsync(
                group.Id,
                new GroupBlacklistCreateRequestDto { UserId = scenario.Owner.Id }));
        Assert.Contains("yourself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BL_A05_AddCurrentOwnerRole_ThrowsTransferFirst()
    {
        var scenario = await GroupTestScenario.CreateAsync("bla05");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeMember: false);
        scenario.LoginAs(GroupActorRole.Owner);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.BlacklistService.AddAsync(
                group.Id,
                new GroupBlacklistCreateRequestDto { UserId = scenario.Owner.Id }));
        Assert.False(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Owner.Id));
    }

    [Fact]
    public async Task BL_A05b_AfterTransfer_CanBlacklistFormerOwnerNowAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("bla05b");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeMember: true);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
        {
            Id = group.Id,
            NewOwnerUserId = scenario.Member.Id,
        });
        scenario.LoginAs(GroupActorRole.Member);

        await scenario.BlacklistService.AddAsync(
            group.Id,
            new GroupBlacklistCreateRequestDto { UserId = scenario.Owner.Id });

        Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Owner.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Owner.Id));
    }

    [Fact]
    public async Task BL_A06_AddTwice_ThrowsAlreadyExists()
    {
        var scenario = await GroupTestScenario.CreateAsync("bla06");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        scenario.LoginAs(GroupActorRole.Owner);
        await scenario.BlacklistService.AddAsync(
            group.Id,
            new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id });

        await Assert.ThrowsAsync<EntityAlreadyExistsException>(() =>
            scenario.BlacklistService.AddAsync(
                group.Id,
                new GroupBlacklistCreateRequestDto { UserId = scenario.Stranger.Id }));
        Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task BL_A09_Add_BlacklistsAdminMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("bla09");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.BlacklistService.AddAsync(
            group.Id,
            new GroupBlacklistCreateRequestDto { UserId = scenario.Admin.Id });

        Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Admin.Id));
        Assert.False(await scenario.MembershipService.IsMemberAsync(group.Id, scenario.Admin.Id));
    }

    #endregion
}

public enum BlacklistAdminAction
{
    Add,
    Remove,
}
