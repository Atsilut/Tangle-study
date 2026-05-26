using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class GroupMatrixTests
{
    // --- Access matrix ---

    public static TheoryData<GroupVisibility, GroupActorRole, GroupReadOperation, GroupExpectedOutcome> AccessMatrixData =>
        new()
        {
            { GroupVisibility.Private, GroupActorRole.Owner, GroupReadOperation.GetGroup, GroupExpectedOutcome.Ok },
            { GroupVisibility.Private, GroupActorRole.Admin, GroupReadOperation.GetGroup, GroupExpectedOutcome.Ok },
            { GroupVisibility.Private, GroupActorRole.Member, GroupReadOperation.GetGroup, GroupExpectedOutcome.Ok },
            { GroupVisibility.Private, GroupActorRole.Stranger, GroupReadOperation.GetGroup, GroupExpectedOutcome.NotFound },
            { GroupVisibility.Private, GroupActorRole.Owner, GroupReadOperation.GetMembers, GroupExpectedOutcome.Ok },
            { GroupVisibility.Private, GroupActorRole.Member, GroupReadOperation.GetMembers, GroupExpectedOutcome.Ok },
            { GroupVisibility.Private, GroupActorRole.Stranger, GroupReadOperation.GetMembers, GroupExpectedOutcome.NotFound },
            { GroupVisibility.Public, GroupActorRole.Stranger, GroupReadOperation.GetGroup, GroupExpectedOutcome.Ok },
            { GroupVisibility.Public, GroupActorRole.Stranger, GroupReadOperation.GetMembers, GroupExpectedOutcome.Ok },
        };

    [Theory]
    [MemberData(nameof(AccessMatrixData))]
    public async Task ReadAccess_Matrix(
        GroupVisibility visibility,
        GroupActorRole actor,
        GroupReadOperation operation,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"acc_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(visibility, includeAdmin: true, includeMember: true);
        scenario.LoginAs(actor);

        if (expected == GroupExpectedOutcome.Ok)
        {
            if (operation == GroupReadOperation.GetGroup)
            {
                var dto = await scenario.GroupService.GetGroupAsync(group.Id);
                Assert.Equal(group.Id, dto.Id);
            }
            else
            {
                var members = await scenario.MembershipService.GetMembersAsync(group.Id);
                Assert.NotNull(members);
                Assert.True(members!.Count >= 1);
            }
        }
        else
        {
            var ex = await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            {
                if (operation == GroupReadOperation.GetGroup)
                    await scenario.GroupService.GetGroupAsync(group.Id);
                else
                    await scenario.MembershipService.GetMembersAsync(group.Id);
            });
            Assert.Equal("Group not found", ex.Message);
        }
    }

    // --- Management matrix ---

    public static TheoryData<GroupActorRole, GroupManagementAction, GroupExpectedOutcome> ManagementMatrixData =>
        new()
        {
            { GroupActorRole.Owner, GroupManagementAction.Update, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, GroupManagementAction.Update, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, GroupManagementAction.Update, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, GroupManagementAction.Update, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, GroupManagementAction.Delete, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, GroupManagementAction.Delete, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, GroupManagementAction.Delete, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, GroupManagementAction.Delete, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Owner, GroupManagementAction.TransferToMember, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, GroupManagementAction.TransferToSelf, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Owner, GroupManagementAction.TransferToStranger, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Admin, GroupManagementAction.TransferToMember, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, GroupManagementAction.TransferToMember, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(ManagementMatrixData))]
    public async Task ManagementAuthorization_Matrix(
        GroupActorRole caller,
        GroupManagementAction action,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"mgmt_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(caller);

        switch (action)
        {
            case GroupManagementAction.Update:
                await AssertManagementOutcomeAsync(
                    expected,
                    () => scenario.GroupService.UpdateGroupAsync(new GroupPatchRequestDto
                    {
                        Id = group.Id,
                        Name = "Updated",
                        Description = "desc",
                        Visibility = GroupVisibility.Public,
                    }),
                    () => scenario.GroupService.UpdateGroupAsync(new GroupPatchRequestDto
                    {
                        Id = group.Id,
                        Name = "x",
                        Description = "y",
                        Visibility = GroupVisibility.Public,
                    }));
                break;
            case GroupManagementAction.Delete:
                await AssertManagementOutcomeAsync(
                    expected,
                    async () =>
                    {
                        await scenario.GroupService.DeleteGroupAsync(group.Id);
                        Assert.Null(await scenario.GroupRepository.GetGroupByIdAsync(group.Id));
                    },
                    () => scenario.GroupService.DeleteGroupAsync(group.Id));
                break;
            case GroupManagementAction.TransferToMember:
                await AssertManagementOutcomeAsync(
                    expected,
                    async () =>
                    {
                        await scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
                        {
                            Id = group.Id,
                            NewOwnerUserId = scenario.Member.Id,
                        });
                        await scenario.AssertMemberRoleAsync(group.Id, scenario.Member.Id, GroupRole.Owner);
                    },
                    () => scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
                    {
                        Id = group.Id,
                        NewOwnerUserId = scenario.Member.Id,
                    }));
                break;
            case GroupManagementAction.TransferToSelf:
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
                    {
                        Id = group.Id,
                        NewOwnerUserId = scenario.Owner.Id,
                    }));
                break;
            case GroupManagementAction.TransferToStranger:
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
                    {
                        Id = group.Id,
                        NewOwnerUserId = scenario.Stranger.Id,
                    }));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static async Task AssertManagementOutcomeAsync(
        GroupExpectedOutcome expected,
        Func<Task> successAct,
        Func<Task> denyAct)
    {
        switch (expected)
        {
            case GroupExpectedOutcome.Ok:
                await successAct();
                break;
            case GroupExpectedOutcome.Unauthorized:
                await Assert.ThrowsAsync<UnauthorizedAccessException>(denyAct);
                break;
            case GroupExpectedOutcome.ArgumentException:
                await Assert.ThrowsAsync<ArgumentException>(denyAct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected), expected, null);
        }
    }

    // --- Remove member matrix ---

    public static TheoryData<GroupActorRole, GroupTargetRole, GroupExpectedOutcome> RemoveMemberMatrixData =>
        new()
        {
            { GroupActorRole.Owner, GroupTargetRole.Member, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, GroupTargetRole.Admin, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, GroupTargetRole.Owner, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Admin, GroupTargetRole.Member, GroupExpectedOutcome.Ok },
            { GroupActorRole.Admin, GroupTargetRole.OtherAdmin, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Admin, GroupTargetRole.Owner, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Member, GroupTargetRole.Self, GroupExpectedOutcome.Ok },
            { GroupActorRole.Member, GroupTargetRole.OtherMember, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, GroupTargetRole.Admin, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Stranger, GroupTargetRole.Member, GroupExpectedOutcome.NotFound },
        };

    [Theory]
    [MemberData(nameof(RemoveMemberMatrixData))]
    public async Task RemoveMemberAuthorization_Matrix(
        GroupActorRole caller,
        GroupTargetRole target,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"rm_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var targetUserId = scenario.ResolveTargetUserId(target, caller);
        scenario.LoginAs(caller);

        switch (expected)
        {
            case GroupExpectedOutcome.Ok:
                await scenario.MembershipService.RemoveMemberAsync(group.Id, targetUserId);
                await scenario.AssertMemberAbsentAsync(group.Id, targetUserId);
                break;
            case GroupExpectedOutcome.Unauthorized:
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.MembershipService.RemoveMemberAsync(group.Id, targetUserId));
                break;
            case GroupExpectedOutcome.ArgumentException:
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    scenario.MembershipService.RemoveMemberAsync(group.Id, targetUserId));
                break;
            case GroupExpectedOutcome.NotFound:
                var ex = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
                    scenario.MembershipService.RemoveMemberAsync(group.Id, targetUserId));
                Assert.Equal("Group not found", ex.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected), expected, null);
        }
    }

    // --- Update role matrix ---

    public static TheoryData<GroupActorRole, GroupTargetRole, GroupRole, GroupExpectedOutcome> UpdateRoleMatrixData =>
        new()
        {
            { GroupActorRole.Owner, GroupTargetRole.Member, GroupRole.Admin, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, GroupTargetRole.Admin, GroupRole.Member, GroupExpectedOutcome.Ok },
            { GroupActorRole.Owner, GroupTargetRole.Owner, GroupRole.Admin, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Owner, GroupTargetRole.Member, GroupRole.Owner, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Owner, GroupTargetRole.Self, GroupRole.Admin, GroupExpectedOutcome.ArgumentException },
            { GroupActorRole.Admin, GroupTargetRole.Member, GroupRole.Admin, GroupExpectedOutcome.Unauthorized },
            { GroupActorRole.Member, GroupTargetRole.Member, GroupRole.Admin, GroupExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(UpdateRoleMatrixData))]
    public async Task UpdateRoleAuthorization_Matrix(
        GroupActorRole caller,
        GroupTargetRole target,
        GroupRole newRole,
        GroupExpectedOutcome expected)
    {
        var scenario = await GroupTestScenario.CreateAsync($"role_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var targetUserId = scenario.ResolveTargetUserId(target, caller);
        scenario.LoginAs(caller);

        var patch = new GroupMemberRolePatchRequestDto { Role = newRole };

        switch (expected)
        {
            case GroupExpectedOutcome.Ok:
                var result = await scenario.MembershipService.UpdateRoleAsync(group.Id, targetUserId, patch);
                Assert.Equal(newRole, result.Role);
                break;
            case GroupExpectedOutcome.Unauthorized:
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    scenario.MembershipService.UpdateRoleAsync(group.Id, targetUserId, patch));
                break;
            case GroupExpectedOutcome.ArgumentException:
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    scenario.MembershipService.UpdateRoleAsync(group.Id, targetUserId, patch));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected), expected, null);
        }
    }

    // --- Multi-step facts ---

    [Fact]
    public async Task TransferOwnership_SwapsOwnerAndPriorOwnerBecomesAdmin()
    {
        var scenario = await GroupTestScenario.CreateAsync("xfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.GroupService.TransferOwnershipAsync(new GroupTransferOwnershipRequestDto
        {
            Id = group.Id,
            NewOwnerUserId = scenario.Member.Id,
        });

        await scenario.AssertMemberRoleAsync(group.Id, scenario.Owner.Id, GroupRole.Admin);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Member.Id, GroupRole.Owner);
    }

    [Fact]
    public async Task DeleteGroup_RemovesAllMemberships()
    {
        var scenario = await GroupTestScenario.CreateAsync("del");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.GroupService.DeleteGroupAsync(group.Id);

        Assert.Null(await scenario.GroupRepository.GetGroupByIdAsync(group.Id));
        Assert.Empty(await scenario.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }
}
