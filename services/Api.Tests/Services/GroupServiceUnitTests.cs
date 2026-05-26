using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupServiceUnitTests
{
    private static GroupCreateRequestDto MakeCreateRequest(string name = "Devs", GroupVisibility visibility = GroupVisibility.Private) =>
        new() { Name = name, Description = "desc", Visibility = visibility };

    // --- CREATE ---

    [Fact]
    public async Task CreateGroup_AddsCreatorAsOwner()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_create");
        scenario.LoginAs(scenario.Owner.Id);

        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest());

        Assert.Equal(1, group.MemberCount);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Owner.Id, GroupRole.Owner);
    }

    // --- GET ---

    [Fact]
    public async Task GetGroup_ThrowsNotFound_WhenPrivateAndNotMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_get_private");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Private));
        scenario.LoginAs(scenario.Stranger.Id);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => scenario.GroupService.GetGroupAsync(group.Id));
    }

    [Fact]
    public async Task GetGroup_ReturnsGroup_WhenPublicAndNotMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_get_public");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest(visibility: GroupVisibility.Public));
        scenario.LoginAs(scenario.Stranger.Id);

        var dto = await scenario.GroupService.GetGroupAsync(group.Id);

        Assert.Equal(group.Id, dto.Id);
        Assert.Equal(GroupVisibility.Public, dto.Visibility);
    }

    // --- UPDATE ---

    [Fact]
    public async Task UpdateGroup_UpdatesDetails_WhenOwner()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_update");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest());

        var updated = await scenario.GroupService.UpdateGroupAsync(new GroupPatchRequestDto
        {
            Id = group.Id,
            Name = "Renamed",
            Description = "new desc",
            Visibility = GroupVisibility.Public,
        });

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(GroupVisibility.Public, updated.Visibility);
    }

    [Fact]
    public async Task UpdateGroup_ThrowsUnauthorized_WhenMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_update_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Member.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.GroupService.UpdateGroupAsync(new GroupPatchRequestDto
            {
                Id = group.Id,
                Name = "x",
                Description = "y",
                Visibility = GroupVisibility.Public,
            }));
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteGroup_RemovesGroupAndMemberships()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_delete");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Owner.Id);

        await scenario.GroupService.DeleteGroupAsync(group.Id);

        Assert.Null(await scenario.GroupRepository.GetGroupByIdAsync(group.Id));
        Assert.Empty(await scenario.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    [Fact]
    public async Task DeleteGroup_ClearsBlacklist()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_delete_bl");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: false);
        await scenario.BlacklistUserAsync(group.Id, scenario.Stranger.Id);
        Assert.True(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
        scenario.LoginAs(scenario.Owner.Id);

        await scenario.GroupService.DeleteGroupAsync(group.Id);

        Assert.Null(await scenario.GroupRepository.GetGroupByIdAsync(group.Id));
        Assert.False(await scenario.BlacklistRepository.ExistsAsync(group.Id, scenario.Stranger.Id));
    }

    [Fact]
    public async Task DeleteGroup_ThrowsUnauthorized_WhenAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_delete_denied");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: false);
        scenario.LoginAs(scenario.Admin.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.GroupService.DeleteGroupAsync(group.Id));
    }

    // --- TRANSFER ---

    [Fact]
    public async Task TransferOwnership_SwapsRoles_WhenTargetIsMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_transfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(scenario.Owner.Id);

        await scenario.GroupService.TransferOwnershipAsync(
            new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Member.Id });

        await scenario.AssertMemberRoleAsync(group.Id, scenario.Owner.Id, GroupRole.Admin);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Member.Id, GroupRole.Owner);
    }

    [Fact]
    public async Task TransferOwnership_ThrowsArgument_WhenTargetIsNotMember()
    {
        var scenario = await GroupTestScenario.CreateAsync("grp_transfer_invalid");
        scenario.LoginAs(scenario.Owner.Id);
        var group = await scenario.GroupService.CreateGroupAsync(MakeCreateRequest());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            scenario.GroupService.TransferOwnershipAsync(
                new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Stranger.Id }));
    }
}
