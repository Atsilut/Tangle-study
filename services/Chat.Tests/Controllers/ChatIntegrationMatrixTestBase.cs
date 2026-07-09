using System.Net;
using Chat.Tests.Infrastructure;
using Tangle.TestSupport.Integration;

namespace Chat.Tests.Controllers;

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

public sealed class ChatIntegrationScenario
{
    public required TestUser Participant { get; init; }
    public required TestUser Stranger { get; init; }
    public required long RoomId { get; init; }
    public TestUser? PlatformOwner { get; init; }
    public TestUser? PlatformMember { get; init; }
    public required TestUser Invitee { get; init; }
}

[Collection(ChatIntegrationTestCollection.Name)]
public abstract class ChatIntegrationMatrixTestBase(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : ChatIntegrationTestBase(postgres, redis)
{
    protected static HttpStatusCode OutcomeStatus(ChatExpectedOutcome expected) =>
        MatrixOutcomeAssertions.ToStatusCode(expected);

    protected static Task AssertUnauthorizedAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.Unauthorized, "Unauthorized access");

    protected static Task AssertChatRoomNotFoundAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Chat room not found");

    protected static Task AssertGroupNotFoundForListAsync(HttpResponseMessage res) =>
        IntegrationAssertions.AssertProblemDetailAsync(res, HttpStatusCode.NotFound, "Group not found");

    protected async Task<ChatIntegrationScenario> CreateDirectScenarioAsync(string prefix)
    {
        var participant = CreateUserForTest(prefix, 1);
        var friend = CreateUserForTest(prefix, 2);
        var stranger = CreateUserForTest(prefix, 3);
        var invitee = CreateUserForTest(prefix, 4);

        AcceptFriendship(participant, friend);
        LoginAs(participant);
        var room = await GetOrCreateDirectRoomAsync(participant, friend.Id);

        return new ChatIntegrationScenario
        {
            Participant = participant,
            Stranger = stranger,
            RoomId = room.Id,
            Invitee = invitee,
        };
    }

    protected async Task<ChatIntegrationScenario> CreateMultiScenarioAsync(string prefix)
    {
        var creator = CreateUserForTest(prefix, 1);
        var other = CreateUserForTest(prefix, 2);
        var stranger = CreateUserForTest(prefix, 3);
        var invitee = CreateUserForTest(prefix, 4);

        LoginAs(creator);
        var room = await CreateMultiRoomAsync(creator, [other.Id]);

        return new ChatIntegrationScenario
        {
            Participant = creator,
            Stranger = stranger,
            RoomId = room.Id,
            Invitee = invitee,
        };
    }

    protected async Task<ChatIntegrationScenario> CreatePlatformGroupScenarioAsync(string prefix)
    {
        var owner = CreateUserForTest(prefix, 1);
        var member = CreateUserForTest(prefix, 2);
        var stranger = CreateUserForTest(prefix, 3);
        var invitee = CreateUserForTest(prefix, 4);

        var (_, _, groupId) = CreateGroupWithMember(owner, member);
        SeedGroupMember(groupId, invitee);

        LoginAs(owner);
        var room = await CreatePlatformGroupChatRoomAsync(owner, groupId, [member.Id]);

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

    protected Task LoginAsActorAsync(ChatIntegrationScenario scenario, ChatActorRole actor)
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
        LoginAs(user);
        return Task.CompletedTask;
    }

    protected Task<ChatIntegrationScenario> CreateScenarioAsync(ChatRoomMatrixKind kind, string prefix) => kind switch
    {
        ChatRoomMatrixKind.Direct => CreateDirectScenarioAsync(prefix),
        ChatRoomMatrixKind.Multi => CreateMultiScenarioAsync(prefix),
        ChatRoomMatrixKind.PlatformGroup => CreatePlatformGroupScenarioAsync(prefix),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
