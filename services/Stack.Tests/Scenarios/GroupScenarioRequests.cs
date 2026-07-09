using System.Net.Http.Json;
using Group.Dto;
using Group.Entities;

namespace Stack.Tests.Scenarios;

public static class GroupScenarioRequests
{
    public const string GroupsBase = "/api/groups";

    public static Task<HttpResponseMessage> PostCreateGroupAsync(
        HttpClient client,
        GroupVisibility visibility = GroupVisibility.Public,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.InvitationOnly,
        GroupInvitePolicy invitePolicy = GroupInvitePolicy.AdminsOnly,
        string? name = null,
        string? description = null) =>
        client.PostAsJsonAsync(
            GroupsBase,
            new GroupCreateRequestDto
            {
                Name = name ?? $"Test_{Guid.NewGuid():N}"[..20],
                Description = description ?? "test group",
                Visibility = visibility,
                JoinPolicy = joinPolicy,
                InvitePolicy = invitePolicy,
            },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PostInviteUserAsync(
        HttpClient client,
        long groupId,
        long inviteeId) =>
        client.PostAsJsonAsync(
            $"{GroupsBase}/{groupId}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = inviteeId },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PostAcceptInvitationAsync(HttpClient client, long invitationId) =>
        client.PostAsync(
            $"/api/invitations/{invitationId}/accept",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetMembersAsync(HttpClient client, long groupId) =>
        client.GetAsync($"{GroupsBase}/{groupId}/members", TestContext.Current.CancellationToken);
}
