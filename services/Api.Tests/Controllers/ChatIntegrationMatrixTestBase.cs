using System.Net;
using Api.Domain.Chat.Dto;
using Api.Domain.Groups.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

public enum ChatActorRole
{
    Participant,
    Stranger,
    PlatformOwner,
    PlatformMember,
}

public enum ChatExpectedOutcome
{
    Ok,
    Unauthorized,
    NotFound,
    BadRequest,
    Conflict,
}

public enum ChatRoomMatrixKind
{
    Direct,
    Multi,
    PlatformGroup,
}

/// <summary>
/// Scaffolds one test scenario per matrix row: a room of a specific kind plus the actors needed
/// (participant, stranger, platform-group owner, platform-group member).
/// </summary>
public sealed class ChatIntegrationScenario
{
    public required UserGetResponseDto Participant { get; init; }
    public required UserGetResponseDto Stranger { get; init; }
    public required long RoomId { get; init; }
    // Only set for platform-group rooms.
    public UserGetResponseDto? PlatformOwner { get; init; }
    public UserGetResponseDto? PlatformMember { get; init; }
    // Extra user available as an "invitee" for AddParticipant rows.
    public required UserGetResponseDto Invitee { get; init; }
}

[Collection(IntegrationTestCollection.Name)]
public abstract class ChatIntegrationMatrixTestBase(PostgresTestcontainerFixture postgres)
    : ChatIntegrationTestBase(postgres)
{
    protected static HttpStatusCode OutcomeStatus(ChatExpectedOutcome expected) => expected switch
    {
        ChatExpectedOutcome.Ok => HttpStatusCode.OK,
        ChatExpectedOutcome.Unauthorized => HttpStatusCode.Unauthorized,
        ChatExpectedOutcome.NotFound => HttpStatusCode.NotFound,
        ChatExpectedOutcome.BadRequest => HttpStatusCode.BadRequest,
        ChatExpectedOutcome.Conflict => HttpStatusCode.Conflict,
        _ => throw new ArgumentOutOfRangeException(nameof(expected), expected, null),
    };

    protected static Task AssertUnauthorizedAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.Unauthorized, "Unauthorized access");

    protected static Task AssertChatRoomNotFoundAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Chat room not found");

    protected static Task AssertGroupNotFoundForListAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");

    /// <summary>Creates a Direct room between Participant and a friend, plus a Stranger and an Invitee.</summary>
    protected async Task<ChatIntegrationScenario> CreateDirectScenarioAsync(string prefix)
    {
        var participant = await CreateUserForTest(prefix, 1);
        var friend = await CreateUserForTest(prefix, 2);
        var stranger = await CreateUserForTest(prefix, 3);
        var invitee = await CreateUserForTest(prefix, 4);

        await AcceptFriendshipAsync(participant, friend);
        var room = await GetOrCreateDirectRoomAsync(participant, friend.Id);

        return new ChatIntegrationScenario
        {
            Participant = participant,
            Stranger = stranger,
            RoomId = room.Id,
            Invitee = invitee,
        };
    }

    /// <summary>Creates a Multi room with Participant as creator, plus a Stranger and an Invitee.</summary>
    protected async Task<ChatIntegrationScenario> CreateMultiScenarioAsync(string prefix)
    {
        var creator = await CreateUserForTest(prefix, 1);
        var other = await CreateUserForTest(prefix, 2);
        var stranger = await CreateUserForTest(prefix, 3);
        var invitee = await CreateUserForTest(prefix, 4);

        var room = await CreateMultiRoomAsync(creator, [other.Id]);

        return new ChatIntegrationScenario
        {
            Participant = creator,
            Stranger = stranger,
            RoomId = room.Id,
            Invitee = invitee,
        };
    }

    /// <summary>
    /// Creates a PlatformGroup room.  PlatformOwner (group owner) and PlatformMember are both room participants;
    /// Stranger is not a group member, and Invitee is a group member not yet in the room.
    /// </summary>
    protected async Task<ChatIntegrationScenario> CreatePlatformGroupScenarioAsync(string prefix)
    {
        var owner = await CreateUserForTest(prefix, 1);
        var member = await CreateUserForTest(prefix, 2);
        var stranger = await CreateUserForTest(prefix, 3);
        var invitee = await CreateUserForTest(prefix, 4);

        var group = await CreateGroupWithMemberAsync(owner, member);
        await GroupIntegrationTestHelpers.SeedGroupMemberAsync(Factory, group.Id, invitee.Id, GroupRole.Member);

        var room = await CreatePlatformGroupChatRoomAsync(owner, group.Id, [member.Id]);

        return new ChatIntegrationScenario
        {
            Participant = owner,
            Stranger = stranger,
            RoomId = room.Id,
            PlatformOwner = owner,
            PlatformMember = member,
            Invitee = invitee,
        };
    }

    protected async Task LoginAsActorAsync(ChatIntegrationScenario scenario, ChatActorRole actor)
    {
        var user = actor switch
        {
            ChatActorRole.Participant => scenario.Participant,
            ChatActorRole.Stranger => scenario.Stranger,
            ChatActorRole.PlatformOwner => scenario.PlatformOwner
                ?? throw new InvalidOperationException("PlatformOwner not set for this scenario"),
            ChatActorRole.PlatformMember => scenario.PlatformMember
                ?? throw new InvalidOperationException("PlatformMember not set for this scenario"),
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null),
        };
        await LoginAs(user);
    }

    protected Task<ChatIntegrationScenario> CreateScenarioAsync(ChatRoomMatrixKind kind, string prefix) => kind switch
    {
        ChatRoomMatrixKind.Direct => CreateDirectScenarioAsync(prefix),
        ChatRoomMatrixKind.Multi => CreateMultiScenarioAsync(prefix),
        ChatRoomMatrixKind.PlatformGroup => CreatePlatformGroupScenarioAsync(prefix),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
