using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupBlacklistJoinMatrixTests
{
    // --- Blacklist and join paths ---

    [Fact]
    public async Task JoinAsync_WhenBlacklisted_Throws()
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
    public async Task ApplyAsync_WhenBlacklisted_Throws()
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
    public async Task InviteAsync_WhenInviteeBlacklisted_ThrowsWithoutRow()
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
    public async Task AcceptAsync_AfterBlacklist_ThrowsNotFound()
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
    public async Task ApproveAsync_AfterBlacklist_ThrowsNotFound()
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
    public async Task AddAsync_KicksExistingMember()
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
    public async Task EnsureNotBlacklisted_WhenListed_Throws()
    {
        var scenario = await GroupTestScenario.CreateAsync("blj07");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.BlacklistService.EnsureNotBlacklistedAsync(group.Id, scenario.Stranger.Id));
        Assert.Contains("blacklisted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinAsync_AfterRemoveFromBlacklist_Succeeds()
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
    public async Task AddAsync_ClearsPendingInvitationAndApplication()
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

    // --- Blacklist admin authorization ---

    public static TheoryData<GroupActorRole, BlacklistAdminAction, GroupExpectedOutcome> AdminAuthorizationData =>
        new()
        {
            { GroupActorRole.Owner, BlacklistAdminAction.Add, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, BlacklistAdminAction.Add, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Admin, BlacklistAdminAction.Remove, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, BlacklistAdminAction.Remove, GroupExpectedOutcome.Ok },
        };

    [Theory]
    [MemberData(nameof(AdminAuthorizationData))]
    public async Task BlacklistAdminAuthorization_Matrix(
        GroupActorRole caller,
        BlacklistAdminAction action,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"blacklist_{Guid.NewGuid():N}"[..8]);
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
    public async Task AddAsync_WhenSelf_ThrowsArgument()
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
    public async Task AddAsync_WhenTargetIsOwner_Throws()
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
    public async Task AddAsync_AfterTransfer_CanBlacklistFormerOwner()
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
    public async Task AddAsync_WhenAlreadyBlacklisted_ThrowsAlreadyExists()
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
    public async Task AddAsync_BlacklistsAdminMember()
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

}
