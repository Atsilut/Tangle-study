using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class ChatRoomControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : ChatIntegrationTestBase(postgres)
{
    // --- DIRECT ---

    [Fact]
    public async Task GetOrCreateDirect_ReturnsSameRoom_OnSecondCall()
    {
        const string testMethodName = nameof(GetOrCreateDirect_ReturnsSameRoom_OnSecondCall);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);

        // Act
        var first = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var second = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ChatRoomKind.Direct, second.Kind);
        Assert.Equal(2, second.Participants.Count);
        Assert.Contains(second.Participants, p => p.UserId == userA.Id);
        Assert.Contains(second.Participants, p => p.UserId == userB.Id);
    }

    [Fact]
    public async Task GetOrCreateDirect_Returns400_WhenNotFriends()
    {
        const string testMethodName = nameof(GetOrCreateDirect_Returns400_WhenNotFriends);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var stranger = await CreateUserForTest(testMethodName, 2);
        await LoginAs(userA);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = stranger.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains("friends", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOrCreateDirect_Returns400_WhenBlocked()
    {
        const string testMethodName = nameof(GetOrCreateDirect_Returns400_WhenBlocked);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        await LoginAs(userA);
        await BlockUserAsync(userB.Id);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = userB.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "block");
    }

    [Fact]
    public async Task GetOrCreateDirect_Returns400_WhenSelfAsOtherUser()
    {
        const string testMethodName = nameof(GetOrCreateDirect_Returns400_WhenSelfAsOtherUser);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        await LoginAs(userA);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/direct",
            new ChatRoomDirectCreateRequestDto { OtherUserId = userA.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOrCreateDirect_AfterPromotion_CreatesNewDirectRoomBetweenSamePair()
    {
        const string testMethodName = nameof(GetOrCreateDirect_AfterPromotion_CreatesNewDirectRoomBetweenSamePair);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var userC = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var firstRoom = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);
        await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{firstRoom.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = userC.Id }, TestContext.Current.CancellationToken);

        // Act
        var secondRoom = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Assert
        Assert.Equal(ChatRoomKind.Direct, secondRoom.Kind);
        Assert.NotEqual(firstRoom.Id, secondRoom.Id);
    }

    // --- MULTI ---

    [Fact]
    public async Task CreateMulti_Returns201_AndAnyParticipantCanAddOthers()
    {
        const string testMethodName = nameof(CreateMulti_Returns201_AndAnyParticipantCanAddOthers);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var userD = await CreateUserForTest(testMethodName, 4);
        var room = await CreateMultiRoomAsync(userA, [userB.Id]);

        // Act
        await LoginAs(userB);
        var addRes = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = userD.Id }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        await LoginAs(userA);
        var getRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}", TestContext.Current.CancellationToken);
        var updated = await getRes.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.Equal(3, updated!.Participants.Count);
        Assert.Contains(updated.Participants, p => p.UserId == userD.Id);
        Assert.All(updated.Participants, p => Assert.Equal(ChatRoomParticipantRole.Member, p.Role));
    }

    [Fact]
    public async Task CreateMulti_Returns400_WhenNoOtherParticipants()
    {
        const string testMethodName = nameof(CreateMulti_Returns400_WhenNoOtherParticipants);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        await LoginAs(userA);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/multi",
            new ChatRoomMultiCreateRequestDto { ParticipantUserIds = [] }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMulti_Returns400_WhenParticipantIsBlocked()
    {
        const string testMethodName = nameof(CreateMulti_Returns400_WhenParticipantIsBlocked);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await LoginAs(userA);
        await BlockUserAsync(userB.Id);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/multi",
            new ChatRoomMultiCreateRequestDto { ParticipantUserIds = [userB.Id] }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "block");
    }

    // --- PARTICIPANTS ---

    [Fact]
    public async Task AddParticipant_ToDirectRoom_PromotesToMulti_AndAddsThirdUser()
    {
        const string testMethodName = nameof(AddParticipant_ToDirectRoom_PromotesToMulti_AndAddsThirdUser);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var userC = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var directRoom = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);

        // Act
        var addRes = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{directRoom.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = userC.Id }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var room = await (await Client.GetAsync($"{ChatRoomsBase}/{directRoom.Id}", TestContext.Current.CancellationToken))
            .Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.Equal(ChatRoomKind.Multi, room!.Kind);
        Assert.Equal(3, room.Participants.Count);
        Assert.All(room.Participants, p => Assert.Equal(ChatRoomParticipantRole.Member, p.Role));
    }

    [Fact]
    public async Task AddParticipant_Returns409_WhenDuplicate()
    {
        const string testMethodName = nameof(AddParticipant_Returns409_WhenDuplicate);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await CreateMultiRoomAsync(userA, [userB.Id]);
        await LoginAs(userA);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = userB.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    // --- PLATFORM GROUP ---

    [Fact]
    public async Task ListPlatformGroupChatRooms_Returns404_ForNonMember()
    {
        const string testMethodName = nameof(ListPlatformGroupChatRooms_Returns404_ForNonMember);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        var group = await CreateGroupWithMemberAsync(owner, member);
        await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id]);
        await LoginAs(stranger);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/chat-rooms", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePlatformGroupChatRoom_AllowsMultipleRoomsPerGroup()
    {
        const string testMethodName = nameof(CreatePlatformGroupChatRoom_AllowsMultipleRoomsPerGroup);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var group = await CreateGroupWithMemberAsync(owner, member);

        // Act
        var first = await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id], title: "General");
        var second = await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id], title: "Off-topic");

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(ChatRoomKind.PlatformGroup, first.Kind);
        Assert.Equal(group.Id, first.PlatformGroupId);
    }

    [Fact]
    public async Task AddParticipant_ToPlatformGroupRoom_RequiresOwner()
    {
        const string testMethodName = nameof(AddParticipant_ToPlatformGroupRoom_RequiresOwner);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var invitee = await CreateUserForTest(testMethodName, 3);
        var group = await CreateGroupWithMemberAsync(owner, member);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, invitee.Id, GroupRole.Member);
        var room = await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id]);

        // Act
        await LoginAs(member);
        var denied = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = invitee.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(denied, HttpStatusCode.Unauthorized);

        // Act
        await LoginAs(owner);
        var allowed = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = invitee.Id }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        var ownerParticipant = room.Participants.Single(p => p.UserId == owner.Id);
        Assert.Equal(ChatRoomParticipantRole.Owner, ownerParticipant.Role);
    }

    [Fact]
    public async Task AddParticipant_ToPlatformGroupRoom_Returns400_WhenInviteeNotGroupMember()
    {
        const string testMethodName = nameof(AddParticipant_ToPlatformGroupRoom_Returns400_WhenInviteeNotGroupMember);

        // Arrange
        var owner = await CreateUserForTest(testMethodName, 1);
        var member = await CreateUserForTest(testMethodName, 2);
        var outsider = await CreateUserForTest(testMethodName, 3);
        var group = await CreateGroupWithMemberAsync(owner, member);
        var room = await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id]);
        await LoginAs(owner);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = outsider.Id }, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailContainsAsync(res, "member of this group");
    }

    // --- LIST / GET / LEAVE ---

    [Fact]
    public async Task ListMyRooms_Returns200_WithCreatedRooms()
    {
        const string testMethodName = nameof(ListMyRooms_Returns200_WithCreatedRooms);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);

        // Act
        var rooms = await ListMyRoomsAsync();

        // Assert
        Assert.NotNull(rooms);
        Assert.Contains(rooms!, r => r.Id == room.Id);
    }

    [Fact]
    public async Task GetRoom_Returns401_ForStranger()
    {
        const string testMethodName = nameof(GetRoom_Returns401_ForStranger);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(stranger);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRoom_Returns404_ForUnknownRoomId()
    {
        const string testMethodName = nameof(GetRoom_Returns404_ForUnknownRoomId);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        await LoginAs(userA);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/99999999", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LeaveRoom_RemovesParticipant_AndStranger401Unchanged()
    {
        const string testMethodName = nameof(LeaveRoom_RemovesParticipant_AndStranger401Unchanged);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Act
        await LoginAs(userA);
        var leaveRes = await LeaveRoomAsync(room.Id);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(leaveRes, HttpStatusCode.NoContent);
        var getRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(getRes, HttpStatusCode.Unauthorized);

        // Act
        await LoginAs(userB);
        var roomDto = await GetRoomAsync(room.Id);

        // Assert
        Assert.DoesNotContain(roomDto.Participants, p => p.UserId == userA.Id);
    }
}
