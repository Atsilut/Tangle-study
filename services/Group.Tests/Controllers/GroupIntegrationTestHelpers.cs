using Group.Db;
using Group.Dto;
using Group.Entities;
using Group.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using GroupInvitationEntity = Group.Entities.GroupInvitation;
using GroupApplicationEntity = Group.Entities.GroupApplication;
using GroupBoardEntity = Group.Entities.GroupBoard;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Scenarios;

namespace Group.Tests.Controllers;

internal static class GroupIntegrationTestHelpers
{
    public const string GroupsBase = GroupScenarioRequests.GroupsBase;

    public static TestUser CreateUser(GroupWebApplicationFactory factory, string nickname, long index = 1)
    {
        var name = index == 1 ? nickname : $"{nickname}_{index}";
        return new(factory.InMemoryUser.SeedUser(name), name);
    }

    public static Task<GroupGetResponseDto> CreateGroupAsAsync(
        HttpClient client,
        TestUser user,
        GroupVisibility visibility = GroupVisibility.Private,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable,
        GroupInvitePolicy invitePolicy = GroupInvitePolicy.AdminsOnly) =>
        GroupApiTestHelpers.CreateGroupAsync(
            client, user.Id, GatewayHeaderAuth.Instance, visibility, joinPolicy, invitePolicy);

    public static void BlockUser(GroupWebApplicationFactory factory, long blockerUserId, long blockedUserId) =>
        factory.InMemoryUser.AddBlock(blockerUserId, blockedUserId);

    public static async Task SeedGroupMemberAsync(
        GroupWebApplicationFactory factory,
        long groupId,
        long userId,
        GroupRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        db.GroupMembers.Add(new GroupMember(groupId, userId, role));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<GroupInvitationEntity> SeedInvitationAsync(
        GroupWebApplicationFactory factory,
        long groupId,
        long inviterId,
        long inviteeId,
        bool isPending = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var invitation = new GroupInvitationEntity(groupId, inviterId, inviteeId);
        if (!isPending) invitation.Ignore();
        db.GroupInvitations.Add(invitation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return invitation;
    }

    public static async Task<GroupApplicationEntity> SeedApplicationAsync(
        GroupWebApplicationFactory factory,
        long groupId,
        long applicantId,
        bool isPending = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var application = new GroupApplicationEntity(groupId, applicantId);
        if (!isPending) application.Ignore();
        db.GroupApplications.Add(application);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return application;
    }

    public static async Task<GroupBoardEntity> SeedBoardAsync(
        GroupWebApplicationFactory factory,
        long groupId,
        string name,
        BoardVisibility visibility,
        BoardWriteability writeability = BoardWriteability.MembersOnly)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var board = new GroupBoardEntity(groupId, name, visibility, writeability: writeability);
        db.GroupBoards.Add(board);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return board;
    }
}
