using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class GroupMatrixTests
{
    #region Access matrix

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
        var scenario = await GroupMatrixScenario.CreateAsync($"acc_{Guid.NewGuid():N}"[..8]);
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

    #endregion

    #region Management matrix

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
        var scenario = await GroupMatrixScenario.CreateAsync($"mgmt_{Guid.NewGuid():N}"[..8]);
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

    #endregion

    #region Remove member matrix

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
        var scenario = await GroupMatrixScenario.CreateAsync($"rm_{Guid.NewGuid():N}"[..8]);
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

    #endregion

    #region Update role matrix

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
        var scenario = await GroupMatrixScenario.CreateAsync($"role_{Guid.NewGuid():N}"[..8]);
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

    #endregion

    #region Multi-step facts

    [Fact]
    public async Task TransferOwnership_SwapsOwnerAndPriorOwnerBecomesAdmin()
    {
        var scenario = await GroupMatrixScenario.CreateAsync("xfer");
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
        var scenario = await GroupMatrixScenario.CreateAsync("del");
        var group = await scenario.SetupGroupAsync(GroupVisibility.Private, includeAdmin: true, includeMember: true);
        scenario.LoginAs(GroupActorRole.Owner);

        await scenario.GroupService.DeleteGroupAsync(group.Id);

        Assert.Null(await scenario.GroupRepository.GetGroupByIdAsync(group.Id));
        Assert.Empty(await scenario.GroupMemberRepository.GetMembersByGroupAsync(group.Id));
    }

    #endregion

    private sealed class GroupMatrixScenario
    {
        private readonly FakeHttpContextAccessor _httpContextAccessor;

        public GroupService GroupService { get; }
        public GroupMembershipService MembershipService { get; }
        public FakeGroupRepository GroupRepository { get; }
        public FakeGroupMemberRepository GroupMemberRepository { get; }
        private readonly FakeUserRepository _userRepository;

        public User Owner { get; private set; } = null!;
        public User Admin { get; private set; } = null!;
        public User AdminB { get; private set; } = null!;
        public User Member { get; private set; } = null!;
        public User MemberB { get; private set; } = null!;
        public User Stranger { get; private set; } = null!;

        private GroupMatrixScenario(FakeHttpContextAccessor httpContextAccessor, DomainServiceTestFactory.Graph graph)
        {
            _httpContextAccessor = httpContextAccessor;
            GroupService = graph.GroupService;
            MembershipService = graph.GroupMembershipService;
            GroupRepository = graph.GroupRepository;
            GroupMemberRepository = graph.GroupMemberRepository;
            _userRepository = graph.UserRepository;
        }

        public static async Task<GroupMatrixScenario> CreateAsync(string nicknamePrefix)
        {
            var http = new FakeHttpContextAccessor("1");
            var graph = DomainServiceTestFactory.Create(http);
            var scenario = new GroupMatrixScenario(http, graph);
            scenario.Owner = await scenario.CreateUserAsync($"{nicknamePrefix}_owner");
            scenario.Admin = await scenario.CreateUserAsync($"{nicknamePrefix}_admin");
            scenario.AdminB = await scenario.CreateUserAsync($"{nicknamePrefix}_adminB");
            scenario.Member = await scenario.CreateUserAsync($"{nicknamePrefix}_member");
            scenario.MemberB = await scenario.CreateUserAsync($"{nicknamePrefix}_memberB");
            scenario.Stranger = await scenario.CreateUserAsync($"{nicknamePrefix}_stranger");
            return scenario;
        }

        private async Task<User> CreateUserAsync(string nickname)
        {
            var user = new User($"{nickname}@test.com", "password", nickname);
            await _userRepository.CreateUserAsync(user);
            return user;
        }

        public void LoginAs(GroupActorRole role) => LoginAs(ResolveActorUserId(role));

        public void LoginAs(long userId) =>
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
            };

        public long ResolveActorUserId(GroupActorRole role) => role switch
        {
            GroupActorRole.Owner => Owner.Id,
            GroupActorRole.Admin => Admin.Id,
            GroupActorRole.Member => Member.Id,
            GroupActorRole.Stranger => Stranger.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };

        public long ResolveTargetUserId(GroupTargetRole target, GroupActorRole caller) => target switch
        {
            GroupTargetRole.Owner => Owner.Id,
            GroupTargetRole.Admin => Admin.Id,
            GroupTargetRole.OtherAdmin => AdminB.Id,
            GroupTargetRole.Member => Member.Id,
            GroupTargetRole.OtherMember => MemberB.Id,
            GroupTargetRole.Self => ResolveActorUserId(caller),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        public async Task<GroupResponseDto> SetupGroupAsync(
            GroupVisibility visibility,
            bool includeAdmin,
            bool includeMember)
        {
            LoginAs(Owner.Id);
            var group = await GroupService.CreateGroupAsync(new GroupCreateRequestDto
            {
                Name = "MatrixGroup",
                Description = "matrix",
                Visibility = visibility,
            });
            if (includeAdmin)
            {
                await MembershipService.AddMemberInternalAsync(group.Id, Admin.Id, GroupRole.Admin);
                await MembershipService.AddMemberInternalAsync(group.Id, AdminB.Id, GroupRole.Admin);
            }
            if (includeMember)
            {
                await MembershipService.AddMemberInternalAsync(group.Id, Member.Id, GroupRole.Member);
                await MembershipService.AddMemberInternalAsync(group.Id, MemberB.Id, GroupRole.Member);
            }
            return group;
        }

        public async Task AssertMemberRoleAsync(long groupId, long userId, GroupRole expected)
        {
            var member = await GroupMemberRepository.GetMemberAsync(groupId, userId);
            Assert.NotNull(member);
            Assert.Equal(expected, member!.Role);
        }

        public async Task AssertMemberAbsentAsync(long groupId, long userId) =>
            Assert.Null(await GroupMemberRepository.GetMemberAsync(groupId, userId));
    }
}

public enum GroupActorRole
{
    Owner,
    Admin,
    Member,
    Stranger,
}

public enum GroupTargetRole
{
    Owner,
    Admin,
    OtherAdmin,
    Member,
    OtherMember,
    Self,
}

public enum GroupReadOperation
{
    GetGroup,
    GetMembers,
}

public enum GroupManagementAction
{
    Update,
    Delete,
    TransferToMember,
    TransferToSelf,
    TransferToStranger,
}

public enum GroupExpectedOutcome
{
    Ok,
    NotFound,
    Unauthorized,
    ArgumentException,
}
