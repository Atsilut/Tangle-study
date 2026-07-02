using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Client;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class ChatMessageControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : ChatIntegrationTestBase(postgres)
{    [Fact]
    public async Task CreateAndListMessages_Returns200_ForDirectRoomParticipants()
    {
        const string testMethodName = nameof(CreateAndListMessages_Returns200_ForDirectRoomParticipants);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Act
        var createRes = await PostMessageAsync(room.Id, "Hello there");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal("Hello there", created.Body);
        Assert.Equal(userA.Id, created.SenderUserId);

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var messages = await listRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(messages);
        var only = Assert.Single(messages);
        Assert.Equal(created.Id, only.Id);
    }

    [Fact]
    public async Task ListMessages_Returns204_WhenRoomHasNoMessages()
    {
        const string testMethodName = nameof(ListMessages_Returns204_WhenRoomHasNoMessages);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMessages_PaginatesWithBeforeCursor()
    {
        const string testMethodName = nameof(ListMessages_PaginatesWithBeforeCursor);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Act
        var first = await (await PostMessageAsync(room.Id, "First")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        var second = await (await PostMessageAsync(room.Id, "Second")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        var third = await (await PostMessageAsync(room.Id, "Third")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(third);
        var pageRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages?before={third.Id}&limit=2", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(pageRes, HttpStatusCode.OK);
        var page = await pageRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2, page.Count);
        Assert.Equal(first.Id, page[0].Id);
        Assert.Equal(second.Id, page[1].Id);
    }

    [Fact]
    public async Task CreateMessage_Returns401_WhenStrangerNotInRoom()
    {
        const string testMethodName = nameof(CreateMessage_Returns401_WhenStrangerNotInRoom);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(stranger);

        // Act
        var createRes = await PostMessageAsync(room.Id, "Intruder");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListMessages_Returns401_WhenStrangerNotInRoom()
    {
        const string testMethodName = nameof(ListMessages_Returns401_WhenStrangerNotInRoom);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var stranger = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await PostMessageAsync(room.Id, "Secret");
        await LoginAs(stranger);

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListMessages_Returns400_WhenBeforeMessageFromAnotherRoom()
    {
        const string testMethodName = nameof(ListMessages_Returns400_WhenBeforeMessageFromAnotherRoom);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        var userC = await CreateUserForTest(testMethodName, 3);
        await AcceptFriendshipAsync(userA, userB);
        await AcceptFriendshipAsync(userA, userC);
        var roomB = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var roomC = await GetOrCreateDirectRoomAsync(userA, userC.Id);
        var msgInC = await (await PostMessageAsync(roomC.Id, "Other room")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(msgInC);

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{roomB.Id}/messages?before={msgInC.Id}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateMessage_Returns400_WhenBodyEmpty(string body)
    {
        const string testMethodName = nameof(CreateMessage_Returns400_WhenBodyEmpty);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);

        // Act
        var res = await PostMessageAsync(room.Id, body);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMessage_Returns400_WhenBodyExceedsMaxLength()
    {
        const string testMethodName = nameof(CreateMessage_Returns400_WhenBodyExceedsMaxLength);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var tooLong = new string('a', ChatMessage.MaxBodyLength + 1);
        await LoginAs(userA);

        // Act
        var res = await PostMessageAsync(room.Id, tooLong);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(null, 50)]   // default limit
    [InlineData(0, 50)]      // below minimum → default
    [InlineData(100, 100)]   // exact max
    [InlineData(200, 100)]   // over max → capped at 100
    public async Task ListMessages_NormalizesLimit(int? requestedLimit, int expectedCount)
    {
        const string testMethodName = nameof(ListMessages_NormalizesLimit);

        // Arrange
        var userA = await CreateUserForTest($"{testMethodName}_{requestedLimit}", 1);
        var userB = await CreateUserForTest($"{testMethodName}_{requestedLimit}", 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);
        for (var i = 0; i < expectedCount + 5; i++) await PostMessageAsync(room.Id, $"msg {i}");

        var url = requestedLimit.HasValue
            ? $"{ChatRoomsBase}/{room.Id}/messages?limit={requestedLimit}"
            : $"{ChatRoomsBase}/{room.Id}/messages";

        // Act
        var res = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var messages = await res.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>(TestContext.Current.CancellationToken);
        Assert.NotNull(messages);
        Assert.Equal(expectedCount, messages.Count);
    }

    [Fact]
    public async Task CreateMessage_Returns201_WhenMediaOnly()
    {
        const string testMethodName = nameof(CreateMessage_Returns201_WhenMediaOnly);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);

        var mediaAssetId = MediaIntegrationTestHelpers.SeedReadyAsset(
            FakeMediaClient,
            MediaIntendedContext.ChatMessage,
            "image/png",
            "chat.png",
            storedSizeBytes: 67);

        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/messages",
            new ChatMessageCreateRequestDto { Body = string.Empty, MediaAssetId = mediaAssetId },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        var created = await res.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(string.Empty, created.Body);
        Assert.NotNull(created.Media);
        Assert.Equal(mediaAssetId, created.Media.Id);
    }

    [Fact]
    public async Task ListMessages_Returns400_WhenBeforeMessageIdNotFound()
    {
        const string testMethodName = nameof(ListMessages_Returns400_WhenBeforeMessageIdNotFound);

        // Arrange
        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await LoginAs(userA);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages?before=99999999", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchMessage_Returns200_WhenSenderAndUnseen()
    {
        const string testMethodName = nameof(PatchMessage_Returns200_WhenSenderAndUnseen);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Hello")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var patchRes = await PatchMessageAsync(room.Id, created.Id, "Updated");
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);
        var patched = await patchRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(patched);
        Assert.Equal("Updated", patched.Body);
        Assert.True(patched.IsEdited);
        Assert.True(patched.CanEdit);
    }

    [Fact]
    public async Task PatchMessage_Returns200_WhenSeenByOtherParticipant()
    {
        const string testMethodName = nameof(PatchMessage_Returns200_WhenSeenByOtherParticipant);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Hello")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        await LoginAs(userB);
        var markSeenRes = await MarkMessagesSeenAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(markSeenRes, HttpStatusCode.NoContent);

        await LoginAs(userA);
        var patchRes = await PatchMessageAsync(room.Id, created.Id, "Still editable");
        await IntegrationAssertions.AssertStatusAsync(patchRes, HttpStatusCode.OK);
        var patched = await patchRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(patched);
        Assert.Equal("Still editable", patched.Body);
        Assert.True(patched.CanEdit);
        Assert.False(patched.CanDelete);
    }

    [Fact]
    public async Task DeleteMessage_SoftDeletes_WhenSenderAndUnseen()
    {
        const string testMethodName = nameof(DeleteMessage_SoftDeletes_WhenSenderAndUnseen);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Delete me")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var deleteRes = await DeleteMessageAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.NoContent);

        var messages = await ListMessagesAsync(room.Id);
        var msg = Assert.Single(messages);
        Assert.True(msg.IsDeleted);
        Assert.Equal(string.Empty, msg.Body);
        Assert.False(msg.CanDelete);
    }

    [Fact]
    public async Task DeleteMessage_Returns400_WhenSeenByOtherParticipant()
    {
        const string testMethodName = nameof(DeleteMessage_Returns400_WhenSeenByOtherParticipant);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Hello")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        await LoginAs(userB);
        var markSeenRes = await MarkMessagesSeenAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(markSeenRes, HttpStatusCode.NoContent);

        await LoginAs(userA);
        var deleteRes = await DeleteMessageAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMessages_DoesNotMarkMessagesSeen()
    {
        const string testMethodName = nameof(ListMessages_DoesNotMarkMessagesSeen);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Unseen")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        await LoginAs(userB);
        await ListMessagesAsync(room.Id);

        await LoginAs(userA);
        var deleteRes = await DeleteMessageAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMessages_ExposesCanEditAndCanDelete_ForOwnUnseenMessage()
    {
        const string testMethodName = nameof(ListMessages_ExposesCanEditAndCanDelete_ForOwnUnseenMessage);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        await PostMessageAsync(room.Id, "Editable");

        var messages = await ListMessagesAsync(room.Id);
        var msg = Assert.Single(messages);
        Assert.True(msg.CanEdit);
        Assert.True(msg.CanDelete);
    }

    [Fact]
    public async Task PatchMessage_RecordsEditHistory_AsThreadedTree()
    {
        const string testMethodName = nameof(PatchMessage_RecordsEditHistory_AsThreadedTree);

        var userA = await CreateUserForTest(testMethodName, 1);
        var userB = await CreateUserForTest(testMethodName, 2);
        await AcceptFriendshipAsync(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var created = await (await PostMessageAsync(room.Id, "Version 1")).Content
            .ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        await PatchMessageAsync(room.Id, created.Id, "Version 2");
        var secondPatch = await PatchMessageAsync(room.Id, created.Id, "Version 3");
        await IntegrationAssertions.AssertStatusAsync(secondPatch, HttpStatusCode.OK);
        var patched = await secondPatch.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(patched);
        Assert.Equal("Version 3", patched.Body);
        Assert.True(patched.IsEdited);
        Assert.NotNull(patched.EditHistory);
        Assert.Equal("Version 2", patched.EditHistory.Body);
        var prior = Assert.Single(patched.EditHistory.PreviousEdits);
        Assert.Equal("Version 1", prior.Body);
        Assert.Empty(prior.PreviousEdits);
    }
}
