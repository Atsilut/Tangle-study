using System.Net;
using System.Net.Http.Json;
using Api.Domain.Chat.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public sealed class ChatRoomAccessIntegrationMatrixTests(PostgresTestcontainerFixture postgres)
    : ChatIntegrationMatrixTestBase(postgres)
{
    public static TheoryData<ChatRoomMatrixKind, ChatActorRole, ChatExpectedOutcome> GetRoomMatrixData =>
        new()
        {
            { ChatRoomMatrixKind.Direct, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Direct, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformOwner, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformMember, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(GetRoomMatrixData))]
    public async Task GetRoom_Matrix(ChatRoomMatrixKind kind, ChatActorRole actor, ChatExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync(kind, $"gr_{Guid.NewGuid():N}"[..8]);
        await LoginAsActorAsync(scenario, actor);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/{scenario.RoomId}", TestContext.Current.CancellationToken);

        // Assert
        if (expected == ChatExpectedOutcome.Ok)
        {
            await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
            var dto = await res.Content.ReadFromJsonAsync<ChatRoomGetResponseDto>(TestContext.Current.CancellationToken);
            Assert.NotNull(dto);
            Assert.Equal(scenario.RoomId, dto.Id);
        }
        else if (expected == ChatExpectedOutcome.Unauthorized) await AssertUnauthorizedAsync(res);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<ChatRoomMatrixKind, ChatActorRole, ChatExpectedOutcome> ListMessagesMatrixData =>
        new()
        {
            { ChatRoomMatrixKind.Direct, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Direct, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformOwner, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformMember, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(ListMessagesMatrixData))]
    public async Task ListMessages_Matrix(ChatRoomMatrixKind kind, ChatActorRole actor, ChatExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync(kind, $"lm_{Guid.NewGuid():N}"[..8]);
        await LoginAsActorAsync(scenario, actor);

        // Act
        var res = await Client.GetAsync($"{ChatRoomsBase}/{scenario.RoomId}/messages", TestContext.Current.CancellationToken);

        // Assert
        if (expected == ChatExpectedOutcome.Ok)
            // Empty room returns 204 for participants — both 200 and 204 are "Ok".
            Assert.True(
                res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
                $"Expected OK/NoContent but got {res.StatusCode}");
        else if (expected == ChatExpectedOutcome.Unauthorized) await AssertUnauthorizedAsync(res);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<ChatRoomMatrixKind, ChatActorRole, ChatExpectedOutcome> PostMessageMatrixData =>
        new()
        {
            { ChatRoomMatrixKind.Direct, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Direct, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformOwner, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformMember, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
        };

    [Theory]
    [MemberData(nameof(PostMessageMatrixData))]
    public async Task PostMessage_Matrix(ChatRoomMatrixKind kind, ChatActorRole actor, ChatExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync(kind, $"pm_{Guid.NewGuid():N}"[..8]);
        await LoginAsActorAsync(scenario, actor);

        // Act
        var res = await PostMessageAsync(scenario.RoomId, "matrix test");

        // Assert
        if (expected == ChatExpectedOutcome.Ok) await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        else if (expected == ChatExpectedOutcome.Unauthorized) await AssertUnauthorizedAsync(res);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<ChatRoomMatrixKind, ChatActorRole, ChatExpectedOutcome> AddParticipantMatrixData =>
        new()
        {
            { ChatRoomMatrixKind.Direct, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Direct, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Participant, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.Multi, ChatActorRole.Stranger, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformOwner, ChatExpectedOutcome.Ok },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.PlatformMember, ChatExpectedOutcome.Unauthorized },
            { ChatRoomMatrixKind.PlatformGroup, ChatActorRole.Stranger, ChatExpectedOutcome.NotFound },
        };

    [Theory]
    [MemberData(nameof(AddParticipantMatrixData))]
    public async Task AddParticipant_Matrix(ChatRoomMatrixKind kind, ChatActorRole actor, ChatExpectedOutcome expected)
    {
        // Arrange
        var scenario = await CreateScenarioAsync(kind, $"ap_{Guid.NewGuid():N}"[..8]);
        await LoginAsActorAsync(scenario, actor);

        // Act
        var res = await Client.PostAsJsonAsync(
            $"{ChatRoomsBase}/{scenario.RoomId}/participants",
            new ChatRoomParticipantAddRequestDto { UserId = scenario.Invitee.Id }, TestContext.Current.CancellationToken);

        // Assert
        if (expected == ChatExpectedOutcome.Ok) await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        else if (expected == ChatExpectedOutcome.Unauthorized) await AssertUnauthorizedAsync(res);
        else if (expected == ChatExpectedOutcome.NotFound) await AssertChatRoomNotFoundAsync(res);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }

    public static TheoryData<ChatActorRole, ChatExpectedOutcome> ListGroupRoomsMatrixData =>
        new()
        {
            { ChatActorRole.PlatformOwner,  ChatExpectedOutcome.Ok       },
            { ChatActorRole.PlatformMember, ChatExpectedOutcome.Ok       },
            { ChatActorRole.Stranger,       ChatExpectedOutcome.NotFound },
        };

    [Theory]
    [MemberData(nameof(ListGroupRoomsMatrixData))]
    public async Task ListGroupChatRooms_Matrix(ChatActorRole actor, ChatExpectedOutcome expected)
    {
        // Arrange
        var prefix = $"lgr_{Guid.NewGuid():N}"[..8];
        var owner = await CreateUserForTest(prefix, 1);
        var member = await CreateUserForTest(prefix, 2);
        var stranger = await CreateUserForTest(prefix, 3);

        var group = await CreateGroupWithMemberAsync(owner, member);
        await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id]);

        var actorUser = actor switch
        {
            ChatActorRole.PlatformOwner  => owner,
            ChatActorRole.PlatformMember => member,
            ChatActorRole.Stranger       => stranger,
            _ => throw new ArgumentOutOfRangeException(nameof(actor)),
        };

        await LoginAs(actorUser);

        // Act
        var res = await Client.GetAsync($"{GroupIntegrationTestHelpers.GroupsBase}/{group.Id}/chat-rooms", TestContext.Current.CancellationToken);

        // Assert
        if (expected == ChatExpectedOutcome.Ok)
            Assert.True(
                res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
                $"Expected OK/NoContent but got {res.StatusCode}");
        else if (expected == ChatExpectedOutcome.NotFound) await AssertGroupNotFoundForListAsync(res);
        else await IntegrationAssertions.AssertStatusAsync(res, OutcomeStatus(expected));
    }
}
