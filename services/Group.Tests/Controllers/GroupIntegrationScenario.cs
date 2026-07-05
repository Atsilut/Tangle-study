using System.Net;
using System.Net.Http.Json;
using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;

namespace Group.Tests.Controllers;

public sealed class GroupIntegrationScenario(HttpClient client, GroupWebApplicationFactory factory)
{
    public TestUser Owner { get; private init; } = null!;
    public TestUser Admin { get; private init; } = null!;
    public TestUser AdminB { get; private init; } = null!;
    public TestUser Member { get; private init; } = null!;
    public TestUser MemberB { get; private init; } = null!;
    public TestUser Stranger { get; private init; } = null!;

    public static GroupIntegrationScenario Create(
        HttpClient client,
        GroupWebApplicationFactory factory,
        string nicknamePrefix)
    {
        return new GroupIntegrationScenario(client, factory)
        {
            Owner = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_owner"),
            Admin = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_admin"),
            AdminB = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_adminB"),
            Member = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_member"),
            MemberB = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_memberB"),
            Stranger = GroupIntegrationTestHelpers.CreateUser(factory, $"{nicknamePrefix}_stranger"),
        };
    }

    public void LoginAs(GroupActorRole role)
    {
        if (role == GroupActorRole.Anonymous)
        {
            GatewayTestAuthHelpers.ClearAuth(client);
            return;
        }

        GroupIntegrationTestHelpers.LoginAs(client, ResolveUser(role));
    }

    public long ResolveUserId(GroupActorRole role) => ResolveUser(role).Id;

    public TestUser ResolveUser(GroupActorRole role) => role switch
    {
        GroupActorRole.Owner => Owner,
        GroupActorRole.Admin => Admin,
        GroupActorRole.Member => Member,
        GroupActorRole.Stranger => Stranger,
        GroupActorRole.Anonymous => throw new InvalidOperationException("Anonymous has no user."),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public long ResolveTargetUserId(GroupTargetRole target, GroupActorRole caller) => target switch
    {
        GroupTargetRole.Owner => Owner.Id,
        GroupTargetRole.Admin => Admin.Id,
        GroupTargetRole.OtherAdmin => AdminB.Id,
        GroupTargetRole.Member => Member.Id,
        GroupTargetRole.OtherMember => MemberB.Id,
        GroupTargetRole.Self => ResolveUserId(caller),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    public async Task<GroupGetResponseDto> SetupGroupAsync(
        GroupVisibility visibility,
        bool includeAdmin = true,
        bool includeMember = true,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable)
    {
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            client, Owner, visibility, joinPolicy);
        if (includeAdmin)
        {
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(factory, group.Id, Admin.Id, GroupRole.Admin);
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(factory, group.Id, AdminB.Id, GroupRole.Admin);
        }
        if (includeMember)
        {
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(factory, group.Id, Member.Id, GroupRole.Member);
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(factory, group.Id, MemberB.Id, GroupRole.Member);
        }
        return group;
    }

    public Task<GroupGetResponseDto> SetupInvitationOnlyGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
        SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.InvitationOnly);

    public Task<GroupGetResponseDto> SetupRequestableGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
        SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.Requestable);

    public async Task<GroupBoardGetResponseDto> CreateBoardAsync(
        long groupId,
        string name,
        BoardVisibility? visibility = null,
        string? description = null)
    {
        var res = await client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/boards",
            new GroupBoardCreateRequestDto
            {
                Name = name,
                Description = description ?? "desc",
                Visibility = visibility,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupBoardGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public async Task<GroupBoardGetResponseDto> SeedBoardAsync(
        long groupId,
        string name,
        BoardVisibility visibility)
    {
        GroupIntegrationTestHelpers.LoginAs(client, Owner);
        return await CreateBoardAsync(groupId, name, visibility);
    }

    public async Task<GroupInvitationCreateResponseDto> InviteStrangerAsync(
        long groupId,
        GroupActorRole inviter = GroupActorRole.Owner)
    {
        LoginAs(inviter);
        var res = await client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = Stranger.Id },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public async Task<GroupApplicationGetResponseDto> ApplyAsStrangerAsync(long groupId)
    {
        LoginAs(GroupActorRole.Stranger);
        var res = await client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/applications",
            content: null,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupApplicationGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public async Task BlacklistUserAsync(long groupId, long userId)
    {
        GroupIntegrationTestHelpers.LoginAs(client, Owner);
        var res = await client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = userId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    public async Task<List<GroupMemberGetResponseDto>> GetMembersAsync(long groupId)
    {
        var res = await client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/members", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<List<GroupMemberGetResponseDto>>(TestContext.Current.CancellationToken))!;
    }

    public async Task AssertMemberRoleAsync(long groupId, long userId, GroupRole expected)
    {
        var members = await GetMembersAsync(groupId);
        var member = members.Single(m => m.UserId == userId);
        Assert.Equal(expected, member.Role);
    }

    public async Task AssertMemberAbsentAsync(long groupId, long userId)
    {
        var members = await GetMembersAsync(groupId);
        Assert.DoesNotContain(members, m => m.UserId == userId);
    }

    public async Task AssertIsMemberAsync(long groupId, long userId, bool expected)
    {
        var members = await GetMembersAsync(groupId);
        Assert.Equal(expected, members.Any(m => m.UserId == userId));
    }
}
