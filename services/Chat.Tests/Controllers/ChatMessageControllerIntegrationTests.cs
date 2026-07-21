using System.Net;
using System.Net.Http.Json;
using Chat.Entities;
using Chat.Dto;
using Chat.Client;
using Chat.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Tests.Controllers;

[Collection(ChatIntegrationTestCollection.Name)]
public sealed class ChatMessageControllerIntegrationTests(PostgresTestcontainerFixture postgres, RedisTestcontainerFixture redis)
    : ChatIntegrationTestBase(postgres, redis)
{    [Fact]
    public async Task CreateAndListMessages_Returns200_ForDirectRoomParticipants()
    {
        const string testMethodName = nameof(CreateAndListMessages_Returns200_ForDirectRoomParticipants);

        // Arrange
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        // Act
        var firstRes = await PostMessageAsync(room.Id, "First");
        await IntegrationAssertions.AssertStatusAsync(firstRes, HttpStatusCode.Created);
        var first = await firstRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        var secondRes = await PostMessageAsync(room.Id, "Second");
        await IntegrationAssertions.AssertStatusAsync(secondRes, HttpStatusCode.Created);
        var second = await secondRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        var thirdRes = await PostMessageAsync(room.Id, "Third");
        await IntegrationAssertions.AssertStatusAsync(thirdRes, HttpStatusCode.Created);
        var third = await thirdRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(stranger);

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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        var stranger = CreateUserForTest(testMethodName, 3);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var secretRes = await PostMessageAsync(room.Id, "Secret");
        await IntegrationAssertions.AssertStatusAsync(secretRes, HttpStatusCode.Created);
        LoginAs(stranger);

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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        var userC = CreateUserForTest(testMethodName, 3);
        AcceptFriendship(userA, userB);
        AcceptFriendship(userA, userC);
        var roomB = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var roomC = await GetOrCreateDirectRoomAsync(userA, userC.Id);
        var otherRoomRes = await PostMessageAsync(roomC.Id, "Other room");
        await IntegrationAssertions.AssertStatusAsync(otherRoomRes, HttpStatusCode.Created);
        var msgInC = await otherRoomRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(userA);

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
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var tooLong = new string('a', ChatMessage.MaxBodyLength + 1);
        LoginAs(userA);

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
        var userA = CreateUserForTest($"{testMethodName}_{requestedLimit}", 1);
        var userB = CreateUserForTest($"{testMethodName}_{requestedLimit}", 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(userA);
        for (var i = 0; i < expectedCount + 5; i++)
        {
            var postRes = await PostMessageAsync(room.Id, $"msg {i}");
            await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        }

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
    public async Task CreateMessage_PersistsMessageAndOutboxRows_Atomically()
    {
        const string testMethodName = nameof(CreateMessage_PersistsMessageAndOutboxRows_Atomically);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var createRes = await PostMessageAsync(room.Id, "outbox atomic");
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var entity = await FindChatMessageEntityAsync(created.Id);
        Assert.NotNull(entity);
        Assert.Equal("outbox atomic", entity.Body);

        var outbox = await GetOutboxMessagesByEntityIdAsync(created.Id);
        Assert.Equal(2, outbox.Count);
        Assert.Contains(outbox, m =>
            m.Destination == Tangle.AspNetCore.Outbox.OutboxDestination.Event
            && m.Target == Chat.Events.RedisEventChannels.ChatMessageCreated
            && m.EntityId == created.Id);
        Assert.Contains(outbox, m =>
            m.Destination == Tangle.AspNetCore.Outbox.OutboxDestination.WorkQueue
            && m.Target == Chat.Queue.WorkQueueStreams.ChatMessageCreated
            && m.EntityId == created.Id);
    }

    [Fact]
    public async Task CreateMessage_Returns201_WhenMediaOnly()
    {
        const string testMethodName = nameof(CreateMessage_Returns201_WhenMediaOnly);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(userA);

        var mediaAssetId = FakeMediaClientTestHelpers.SeedReadyAsset(
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
        Assert.True(FakeMediaClient.IsLinkedToChatMessage(mediaAssetId, created.Id));

        var entity = await FindChatMessageEntityAsync(created.Id);
        Assert.NotNull(entity);
        var outbox = await GetOutboxMessagesByEntityIdAsync(created.Id);
        Assert.Equal(2, outbox.Count);
    }

    [Fact]
    public async Task CreateMessage_DispatchesOutboxRows_ViaRedis()
    {
        const string testMethodName = nameof(CreateMessage_DispatchesOutboxRows_ViaRedis);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);

        var createRes = await PostMessageAsync(room.Id, "dispatch me");
        await IntegrationAssertions.AssertStatusAsync(createRes, HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var outbox = await IntegrationTestPolling.PollUntilAsync(
            async ct => await GetOutboxMessagesByEntityIdAsync(created.Id),
            rows => rows.Count == 2 && rows.All(r => r.ProcessedAt is not null),
            timeout: TimeSpan.FromSeconds(10),
            delay: TimeSpan.FromMilliseconds(200),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(outbox, row =>
        {
            Assert.NotNull(row.ProcessedAt);
            Assert.Null(row.DeadLetteredAt);
            Assert.Equal(created.Id, row.EntityId);
        });
    }

    [Fact]
    public async Task CreateMessage_Compensates_WhenMediaLinkFails()
    {
        const string testMethodName = nameof(CreateMessage_Compensates_WhenMediaLinkFails);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(userA);

        var mediaAssetId = FakeMediaClientTestHelpers.SeedReadyAsset(
            FakeMediaClient,
            MediaIntendedContext.ChatMessage,
            "image/png",
            "chat-fail.png",
            storedSizeBytes: 67);
        FakeMediaClient.FailNextLink(new ArgumentException("Simulated media link failure"));

        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{room.Id}/messages",
            new ChatMessageCreateRequestDto { Body = "should roll back", MediaAssetId = mediaAssetId },
            TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
        Assert.False(FakeMediaClient.IsAssetLinked(mediaAssetId));

        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.NoContent);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Chat.Db.ChatDbContext>();
        Assert.Empty(await db.ChatMessages.AsNoTracking().ToListAsync());
        Assert.Empty(await db.OutboxMessages.AsNoTracking()
            .Where(m => m.ProcessedAt == null && m.DeadLetteredAt == null)
            .ToListAsync());
    }

    [Fact]
    public async Task ListMessages_Returns400_WhenBeforeMessageIdNotFound()
    {
        const string testMethodName = nameof(ListMessages_Returns400_WhenBeforeMessageIdNotFound);

        // Arrange
        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        LoginAs(userA);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages?before=99999999", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchMessage_Returns200_WhenSenderAndUnseen()
    {
        const string testMethodName = nameof(PatchMessage_Returns200_WhenSenderAndUnseen);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Hello");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
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

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Hello");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        LoginAs(userB);
        var markSeenRes = await MarkMessagesSeenAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(markSeenRes, HttpStatusCode.NoContent);

        LoginAs(userA);
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

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Delete me");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
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

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Hello");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        LoginAs(userB);
        var markSeenRes = await MarkMessagesSeenAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(markSeenRes, HttpStatusCode.NoContent);

        LoginAs(userA);
        var deleteRes = await DeleteMessageAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMessages_DoesNotMarkMessagesSeen()
    {
        const string testMethodName = nameof(ListMessages_DoesNotMarkMessagesSeen);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Unseen");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        LoginAs(userB);
        await ListMessagesAsync(room.Id);

        LoginAs(userA);
        var deleteRes = await DeleteMessageAsync(room.Id, created.Id);
        await IntegrationAssertions.AssertStatusAsync(deleteRes, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMessages_ExposesCanEditAndCanDelete_ForOwnUnseenMessage()
    {
        const string testMethodName = nameof(ListMessages_ExposesCanEditAndCanDelete_ForOwnUnseenMessage);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Editable");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);

        var messages = await ListMessagesAsync(room.Id);
        var msg = Assert.Single(messages);
        Assert.True(msg.CanEdit);
        Assert.True(msg.CanDelete);
    }

    [Fact]
    public async Task PatchMessage_RecordsEditHistory_AsThreadedTree()
    {
        const string testMethodName = nameof(PatchMessage_RecordsEditHistory_AsThreadedTree);

        var userA = CreateUserForTest(testMethodName, 1);
        var userB = CreateUserForTest(testMethodName, 2);
        AcceptFriendship(userA, userB);
        var room = await GetOrCreateDirectRoomAsync(userA, userB.Id);
        var postRes = await PostMessageAsync(room.Id, "Version 1");
        await IntegrationAssertions.AssertStatusAsync(postRes, HttpStatusCode.Created);
        var created = await postRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>(TestContext.Current.CancellationToken);
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
