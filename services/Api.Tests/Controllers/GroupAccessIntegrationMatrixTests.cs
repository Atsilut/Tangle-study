using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupAccessIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : GroupIntegrationMatrixTestBase(postgres)
{
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
        // Arrange
        var scenario = await CreateScenarioAsync($"acc_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(visibility, includeAdmin: true, includeMember: true);
        await scenario.LoginAsAsync(actor);

        var path = operation == GroupReadOperation.GetGroup
            ? $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}"
            : $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members";

        // Act
        var res = await Client.GetAsync(path);

        // Assert
        if (expected == GroupExpectedOutcome.Ok)
        {
            await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
            if (operation == GroupReadOperation.GetGroup)
            {
                var dto = await res.Content.ReadFromJsonAsync<GroupResponseDto>();
                Assert.Equal(group.Id, dto!.Id);
            }
            else
            {
                var members = await res.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>();
                Assert.NotNull(members);
                Assert.True(members!.Count >= 1);
            }
        }
        else
        {
            await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
        }
    }

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
        // Arrange
        var scenario = await CreateScenarioAsync($"mgmt_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        await scenario.LoginAsAsync(caller);

        // Act
        HttpResponseMessage res;
        switch (action)
        {
            case GroupManagementAction.Update:
                res = await Client.PatchAsJsonAsync(GroupIntegrationTestHelpers.GroupsBase, new GroupPatchRequestDto
                {
                    Id = group.Id,
                    Name = expected == GroupExpectedOutcome.Ok ? "Updated" : "x",
                    Description = "desc",
                    Visibility = GroupVisibility.Public,
                });
                break;
            case GroupManagementAction.Delete:
                res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");
                break;
            case GroupManagementAction.TransferToMember:
                res = await Client.PatchAsJsonAsync($"{GroupIntegrationTestHelpers.GroupsBase}/transfer",
                    new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Member.Id });
                break;
            case GroupManagementAction.TransferToSelf:
                res = await Client.PatchAsJsonAsync($"{GroupIntegrationTestHelpers.GroupsBase}/transfer",
                    new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Owner.Id });
                break;
            case GroupManagementAction.TransferToStranger:
                res = await Client.PatchAsJsonAsync($"{GroupIntegrationTestHelpers.GroupsBase}/transfer",
                    new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Stranger.Id });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        // Assert
        switch (action)
        {
            case GroupManagementAction.Update:
                if (expected == GroupExpectedOutcome.Ok)
                    await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
                else
                    await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
                break;
            case GroupManagementAction.Delete:
                if (expected == GroupExpectedOutcome.Ok)
                {
                    await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
                    await scenario.LoginAsAsync(GroupActorRole.Owner);
                    var get = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");
                    await IntegrationAssertions.AssertStatusAsync(get, HttpStatusCode.NotFound);
                }
                else
                    await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
                break;
            case GroupManagementAction.TransferToMember:
                if (expected == GroupExpectedOutcome.Ok)
                {
                    await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
                    await scenario.AssertMemberRoleAsync(group.Id, scenario.Member.Id, GroupRole.Owner);
                }
                else
                    await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
                break;
            case GroupManagementAction.TransferToSelf:
            case GroupManagementAction.TransferToStranger:
                await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
                break;
        }
    }

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
        // Arrange
        var scenario = await CreateScenarioAsync($"rm_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var targetUserId = scenario.ResolveTargetUserId(target, caller);
        await scenario.LoginAsAsync(caller);

        // Act
        var res = await Client.DeleteAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{targetUserId}");

        // Assert
        if (expected == GroupExpectedOutcome.Ok)
        {
            await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);
            await scenario.LoginAsAsync(GroupActorRole.Owner);
            var members = await scenario.GetMembersAsync(group.Id);
            Assert.DoesNotContain(members, m => m.UserId == targetUserId);
        }
        else
            await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

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
        // Arrange
        var scenario = await CreateScenarioAsync($"role_{Guid.NewGuid():N}"[..8]);
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        var targetUserId = scenario.ResolveTargetUserId(target, caller);
        await scenario.LoginAsAsync(caller);

        // Act
        var res = await Client.PatchAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/members/{targetUserId}",
            new GroupMemberRolePatchRequestDto { Role = newRole });

        // Assert
        if (expected == GroupExpectedOutcome.Ok)
        {
            await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
            var body = await res.Content.ReadFromJsonAsync<GroupMemberResponseDto>();
            Assert.Equal(newRole, body!.Role);
        }
        else
            await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    [Fact]
    public async Task TransferOwnership_SwapsOwnerAndPriorOwnerBecomesAdmin()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("xfer");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: false, includeMember: true);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.PatchAsJsonAsync($"{GroupIntegrationTestHelpers.GroupsBase}/transfer",
            new GroupTransferOwnershipRequestDto { Id = group.Id, NewOwnerUserId = scenario.Member.Id });

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Owner.Id, GroupRole.Admin);
        await scenario.AssertMemberRoleAsync(group.Id, scenario.Member.Id, GroupRole.Owner);
    }

    [Fact]
    public async Task DeleteGroup_RemovesAllMemberships()
    {
        // Arrange
        var scenario = await CreateScenarioAsync("del");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        await scenario.LoginAsAsync(GroupActorRole.Owner);

        // Act
        var res = await Client.DeleteAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NoContent);

        var get = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}");
        await IntegrationAssertions.AssertStatusAsync(get, HttpStatusCode.NotFound);
    }
}
