using System.Net;
using System.Net.Http.Json;
using Group.Dto;
using Group.Entities;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Integration;

namespace Tangle.TestSupport.Scenarios;

public static class GroupApiTestHelpers
{
    public const string GroupsBase = GroupScenarioRequests.GroupsBase;

    public static async Task<GroupGetResponseDto> CreateGroupAsync(
        HttpClient client,
        long ownerUserId,
        ITestAuth auth,
        GroupVisibility visibility = GroupVisibility.Public,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.InvitationOnly,
        GroupInvitePolicy invitePolicy = GroupInvitePolicy.AdminsOnly)
    {
        await auth.AuthenticateAsync(client, ownerUserId, TestContext.Current.CancellationToken);
        var res = await GroupScenarioRequests.PostCreateGroupAsync(
            client, visibility, joinPolicy, invitePolicy);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<GroupGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task<GroupInvitationCreateResponseDto> InviteUserAsync(
        HttpClient client,
        long inviterUserId,
        long groupId,
        long inviteeId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, inviterUserId, TestContext.Current.CancellationToken);
        var res = await GroupScenarioRequests.PostInviteUserAsync(client, groupId, inviteeId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task AcceptInvitationAsync(
        HttpClient client,
        long inviteeUserId,
        long invitationId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, inviteeUserId, TestContext.Current.CancellationToken);
        var res = await GroupScenarioRequests.PostAcceptInvitationAsync(client, invitationId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
    }

    public static async Task<List<GroupMemberGetResponseDto>> GetMembersAsync(
        HttpClient client,
        long asUserId,
        long groupId,
        ITestAuth auth)
    {
        await auth.AuthenticateAsync(client, asUserId, TestContext.Current.CancellationToken);
        var res = await GroupScenarioRequests.GetMembersAsync(client, groupId);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<List<GroupMemberGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }
}
