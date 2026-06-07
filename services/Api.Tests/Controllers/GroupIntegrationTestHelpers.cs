using System.Net;
using System.Net.Http.Json;
using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using GroupInvitationEntity = Api.Domain.Groups.Domain.GroupInvitation;
using GroupApplicationEntity = Api.Domain.Groups.Domain.GroupApplication;
using GroupBoardEntity = Api.Domain.Groups.Domain.GroupBoard;

namespace Api.Tests.Controllers;

internal static class GroupIntegrationTestHelpers
{
    public const string DefaultPassword = IntegrationTestAuthHelpers.DefaultPassword;
    public const string GroupsBase = "/api/groups";

    public static Task<UserGetResponseDto> CreateUserForTestAsync(
        HttpClient client,
        string testMethodName,
        long index = 1,
        string? password = null) =>
        IntegrationTestAuthHelpers.CreateUserForTestAsync(client, testMethodName, index, password);

    public static Task LoginAsAsync(HttpClient client, UserGetResponseDto user, string? password = null) =>
        IntegrationTestAuthHelpers.LoginAsAsync(client, user, password);

    public static async Task<GroupResponseDto> CreateGroupAsAsync(
        HttpClient client,
        UserGetResponseDto user,
        GroupVisibility visibility = GroupVisibility.Private,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable)
    {
        await LoginAsAsync(client, user);
        var res = await client.PostAsJsonAsync(GroupsBase, new GroupCreateRequestDto
        {
            Name = $"Group_{Guid.NewGuid():N}".Substring(0, 20),
            Description = "test group",
            Visibility = visibility,
            JoinPolicy = joinPolicy,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<GroupResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static async Task BlockUserAsync(HttpClient client, long blockedUserId)
    {
        var res = await client.PostAsJsonAsync("/api/users/blocks",
            new UserBlockCreateRequestDto { BlockedUserId = blockedUserId },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    public static async Task SeedGroupMemberAsync(
        ApiWebApplicationFactory factory,
        long groupId,
        long userId,
        GroupRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GroupMembers.Add(new GroupMember(groupId, userId, role));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<GroupInvitationEntity> SeedInvitationAsync(
        ApiWebApplicationFactory factory,
        long groupId,
        long inviterId,
        long inviteeId,
        bool isPending = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = new GroupInvitationEntity(groupId, inviterId, inviteeId);
        if (!isPending) invitation.Ignore();
        db.GroupInvitations.Add(invitation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return invitation;
    }

    public static async Task<GroupApplicationEntity> SeedApplicationAsync(
        ApiWebApplicationFactory factory,
        long groupId,
        long applicantId,
        bool isPending = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = new GroupApplicationEntity(groupId, applicantId);
        if (!isPending) application.Ignore();
        db.GroupApplications.Add(application);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return application;
    }

    public static async Task<GroupBoardEntity> SeedBoardAsync(
        ApiWebApplicationFactory factory,
        long groupId,
        string name,
        BoardVisibility visibility)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var board = new GroupBoardEntity(groupId, name, visibility);
        db.GroupBoards.Add(board);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return board;
    }
}
