using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class ChatMessageControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : ChatIntegrationTestBase(postgres)
{
    [Fact]
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
        var created = await createRes.Content.ReadFromJsonAsync<ChatMessageGetResponseDto>();
        Assert.Equal("Hello there", created!.Body);
        Assert.Equal(userA.Id, created.SenderUserId);

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(listRes, HttpStatusCode.OK);
        var messages = await listRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>();
        Assert.Single(messages!);
        Assert.Equal(created.Id, messages![0].Id);
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
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages");

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
        var first = await (await PostMessageAsync(room.Id, "First")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>();
        var second = await (await PostMessageAsync(room.Id, "Second")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>();
        var third = await (await PostMessageAsync(room.Id, "Third")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>();
        var pageRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages?before={third!.Id}&limit=2");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(pageRes, HttpStatusCode.OK);
        var page = await pageRes.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>();
        Assert.Equal(2, page!.Count);
        Assert.Equal(first!.Id, page[0].Id);
        Assert.Equal(second!.Id, page[1].Id);
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
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages");

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
        var msgInC = await (await PostMessageAsync(roomC.Id, "Other room")).Content.ReadFromJsonAsync<ChatMessageGetResponseDto>();

        // Act
        var listRes = await Client.GetAsync($"{ChatRoomsBase}/{roomB.Id}/messages?before={msgInC!.Id}");

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
        var res = await Client.GetAsync(url);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var messages = await res.Content.ReadFromJsonAsync<List<ChatMessageGetResponseDto>>();
        Assert.Equal(expectedCount, messages!.Count);
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
        var res = await Client.GetAsync($"{ChatRoomsBase}/{room.Id}/messages?before=99999999");

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }
}
