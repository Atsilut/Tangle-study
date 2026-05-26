using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupMembershipServiceUnitTests
{
    private static GroupCreateRequestDto MakeCreateRequest(string name = "g", GroupVisibility visibility = GroupVisibility.Private) =>
        new() { Name = name, Description = "d", Visibility = visibility };

    // --- ROLE ---

    [Fact]
    public async Task UpdateRole_PromotesMemberToAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_promote");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Owner.Id);

        var result = await scenario.MembershipService.UpdateRoleAsync(
            group.Id,
            scenario.Member.Id,
            new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin });

        Assert.Equal(GroupRole.Admin, result.Role);
    }

    [Fact]
    public async Task UpdateRole_ThrowsUnauthorized_WhenCallerIsAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_promote_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(scenario.Admin.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.MembershipService.UpdateRoleAsync(
                group.Id,
                scenario.Member.Id,
                new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin }));
    }

    [Fact]
    public async Task UpdateRole_ThrowsArgument_WhenTargetIsOwner()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_promote_owner");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.MembershipService.UpdateRoleAsync(
                group.Id,
                scenario.Owner.Id,
                new GroupMemberRolePatchRequestDto { Role = GroupRole.Admin }));
    }

    // --- REMOVE ---

    [Fact]
    public async Task RemoveMember_RemovesMembership_WhenSelfLeave()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_leave");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Member.Id);

        await scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.Member.Id);

        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
    }

    [Fact]
    public async Task RemoveMember_ThrowsArgument_WhenOwnerLeaves()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_owner_leave");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.Owner.Id));
    }

    [Fact]
    public async Task RemoveMember_RemovesMember_WhenAdminKicksMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_kick");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(scenario.Admin.Id);

        await scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.Member.Id);

        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Member.Id);
    }

    [Fact]
    public async Task RemoveMember_RemovesAdmin_WhenOwnerKicksAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_kick_admin");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        scenario.LoginAs(scenario.Owner.Id);

        await scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.Admin.Id);

        await scenario.AssertMemberAbsentAsync(group.Id, scenario.Admin.Id);
    }

    [Fact]
    public async Task RemoveMember_ThrowsUnauthorized_WhenAdminKicksAnotherAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_kick_admin_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        scenario.LoginAs(scenario.Admin.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.AdminB.Id));
    }

    [Fact]
    public async Task RemoveMember_ThrowsUnauthorized_WhenMemberKicksAnother()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_kick_peer_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Member.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.MembershipService.RemoveMemberAsync(group.Id, scenario.MemberB.Id));
    }

    // --- GET MEMBERS ---

    [Fact]
    public async Task GetMembers_ReturnsList_OrderedByRoleThenJoined()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_list");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Public, includeAdmin: true, includeMember: true);
        scenario.LoginAs(scenario.Owner.Id);

        var members = await scenario.MembershipService.GetMembersAsync(group.Id);

        Assert.NotNull(members);
        Assert.Equal(5, members!.Count);
        Assert.Equal(GroupRole.Owner, members[0].Role);
        Assert.Equal(GroupRole.Admin, members[1].Role);
        Assert.Equal(GroupRole.Admin, members[2].Role);
        Assert.Equal(GroupRole.Member, members[3].Role);
        Assert.Equal(GroupRole.Member, members[4].Role);
    }

    [Fact]
    public async Task GetMembers_ThrowsNotFound_WhenPrivateGroupAndNonMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("mem_list_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: false);
        scenario.LoginAs(scenario.Stranger.Id);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.MembershipService.GetMembersAsync(group.Id));
    }
}
