using Group.Dto;
using Group.Entities;
using Stack.Tests.Scenarios;
using Tangle.TestSupport.Auth;
using Stack.Tests.Scenarios;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

internal static class GroupHarnessHelpers
{
    public const string GroupsBase = GroupScenarioRequests.GroupsBase;

    private static ITestAuth Auth => HarnessJwtAuth.Instance;

    public static Task<GroupGetResponseDto> CreateGroupAsync(
        HttpClient client,
        UserGetResponseDto owner,
        GroupVisibility visibility = GroupVisibility.Public,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.InvitationOnly,
        GroupInvitePolicy invitePolicy = GroupInvitePolicy.AdminsOnly) =>
        GroupApiTestHelpers.CreateGroupAsync(
            client, owner.Id, Auth, visibility, joinPolicy, invitePolicy);

    public static Task<GroupInvitationCreateResponseDto> InviteUserAsync(
        HttpClient client,
        UserGetResponseDto inviter,
        long groupId,
        long inviteeId) =>
        GroupApiTestHelpers.InviteUserAsync(client, inviter.Id, groupId, inviteeId, Auth);

    public static Task AcceptInvitationAsync(
        HttpClient client,
        UserGetResponseDto invitee,
        long invitationId) =>
        GroupApiTestHelpers.AcceptInvitationAsync(client, invitee.Id, invitationId, Auth);

    public static Task<List<GroupMemberGetResponseDto>> GetMembersAsync(
        HttpClient client,
        UserGetResponseDto asUser,
        long groupId) =>
        GroupApiTestHelpers.GetMembersAsync(client, asUser.Id, groupId, Auth);
}
