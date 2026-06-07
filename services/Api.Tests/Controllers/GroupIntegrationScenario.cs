using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class GroupIntegrationScenario
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public UserGetResponseDto Owner { get; private init; } = null!;
    public UserGetResponseDto Admin { get; private init; } = null!;
    public UserGetResponseDto AdminB { get; private init; } = null!;
    public UserGetResponseDto Member { get; private init; } = null!;
    public UserGetResponseDto MemberB { get; private init; } = null!;
    public UserGetResponseDto Stranger { get; private init; } = null!;

    private GroupIntegrationScenario(HttpClient client, ApiWebApplicationFactory factory)
    {
        _client = client;
        _factory = factory;
    }

    public static async Task<GroupIntegrationScenario> CreateAsync(
        HttpClient client,
        ApiWebApplicationFactory factory,
        string nicknamePrefix)
    {
        var scenario = new GroupIntegrationScenario(client, factory)
        {
            Owner = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_owner", 1),
            Admin = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_admin", 1),
            AdminB = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_adminB", 1),
            Member = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_member", 1),
            MemberB = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_memberB", 1),
            Stranger = await GroupIntegrationTestHelpers.CreateUserForTestAsync(client, $"{nicknamePrefix}_stranger", 1),
        };
        return scenario;
    }

    public async Task LoginAsAsync(GroupActorRole role)
    {
        if (role == GroupActorRole.Anonymous)
        {
            _client.DefaultRequestHeaders.Authorization = null;
            return;
        }

        await GroupIntegrationTestHelpers.LoginAsAsync(_client, ResolveUser(role));
    }

    public long ResolveUserId(GroupActorRole role) => ResolveUser(role).Id;

    public UserGetResponseDto ResolveUser(GroupActorRole role) => role switch
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

    public async Task<GroupResponseDto> SetupGroupAsync(
        GroupVisibility visibility,
        bool includeAdmin = true,
        bool includeMember = true,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable)
    {
        var group = await GroupIntegrationTestHelpers.CreateGroupAsAsync(
            _client, Owner, visibility, joinPolicy);
        if (includeAdmin)
        {
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(_factory, group.Id, Admin.Id, GroupRole.Admin);
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(_factory, group.Id, AdminB.Id, GroupRole.Admin);
        }
        if (includeMember)
        {
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(_factory, group.Id, Member.Id, GroupRole.Member);
            await GroupIntegrationTestHelpers.SeedGroupMemberAsync(_factory, group.Id, MemberB.Id, GroupRole.Member);
        }
        return group;
    }

    public Task<GroupResponseDto> SetupInvitationOnlyGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.InvitationOnly);

    public Task<GroupResponseDto> SetupRequestableGroupAsync(
        bool includeAdmin = true,
        bool includeMember = false) =>
SetupGroupAsync(
            GroupVisibility.Public,
            includeAdmin: includeAdmin,
            includeMember: includeMember,
            joinPolicy: GroupJoinPolicy.Requestable);

    public async Task<GroupBoardResponseDto> CreateBoardAsync(
        long groupId,
        string name,
        BoardVisibility? visibility = null,
        string? description = null)
    {
        var res = await _client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/boards",
            new GroupBoardCreateRequestDto
            {
                Name = name,
                Description = description ?? "desc",
                Visibility = visibility,
            });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupBoardResponseDto>())!;
    }

    public async Task<GroupBoardResponseDto> SeedBoardAsync(
        long groupId,
        string name,
        BoardVisibility visibility)
    {
        await GroupIntegrationTestHelpers.LoginAsAsync(_client, Owner);
        return await CreateBoardAsync(groupId, name, visibility);
    }

    public async Task<GroupInvitationCreateResponseDto> InviteStrangerAsync(
        long groupId,
        GroupActorRole inviter = GroupActorRole.Owner)
    {
        await LoginAsAsync(inviter);
        var res = await _client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/invitations",
            new GroupInvitationCreateRequestDto { InviteeId = Stranger.Id });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupInvitationCreateResponseDto>())!;
    }

    public async Task<GroupApplicationResponseDto> ApplyAsStrangerAsync(long groupId)
    {
        await LoginAsAsync(GroupActorRole.Stranger);
        var res = await _client.PostAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/applications",
            content: null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupApplicationResponseDto>())!;
    }

    public async Task BlacklistUserAsync(long groupId, long userId)
    {
        await GroupIntegrationTestHelpers.LoginAsAsync(_client, Owner);
        var res = await _client.PostAsJsonAsync(
            $"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/blacklist",
            new GroupBlacklistCreateRequestDto { UserId = userId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    public async Task<List<GroupMemberResponseDto>> GetMembersAsync(long groupId)
    {
        var res = await _client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{groupId}/members");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<List<GroupMemberResponseDto>>())!;
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
