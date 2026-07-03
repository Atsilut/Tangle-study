using System.Security.Claims;
using Chat.Client;
using Chat.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Chat.Tests.Infrastructure;

/// <summary>
/// Configurable in-memory monolith access checks for Chat integration and unit tests.
/// </summary>
public sealed class InMemoryMonolithAccessClient(IHttpContextAccessor httpContextAccessor) : IMonolithAccessClient
{
    private long _nextUserId = 1;
    private long _nextGroupId = 1;

    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public HashSet<(long Low, long High)> Friendships { get; } = [];
    public HashSet<(long Blocker, long Blocked)> Blocks { get; } = [];
    public HashSet<long> Groups { get; } = [];
    public HashSet<(long GroupId, long UserId)> GroupMembers { get; } = [];

    public TestUser CreateUser(string nickname)
    {
        var id = _nextUserId++;
        Users.Add(id);
        Nicknames[id] = nickname;
        return new TestUser(id, nickname);
    }

    public long CreateGroup()
    {
        var id = _nextGroupId++;
        Groups.Add(id);
        return id;
    }

    public void AddFriendship(long userAId, long userBId)
    {
        var (low, high) = CanonicalPair(userAId, userBId);
        Friendships.Add((low, high));
    }

    public void AddGroupMember(long groupId, long userId)
    {
        Groups.Add(groupId);
        Users.Add(userId);
        GroupMembers.Add((groupId, userId));
    }

    public void AddBlock(long blockerUserId, long blockedUserId) =>
        Blocks.Add((blockerUserId, blockedUserId));

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new ArgumentException("User not found");
        return Task.CompletedTask;
    }

    public Task EnsureUsersExistAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            if (!Users.Contains(userId))
                throw new ArgumentException("User not found");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var result = userIds
            .Distinct()
            .ToDictionary(id => id, id => Nicknames.GetValueOrDefault(id, "Deleted User"));
        return Task.FromResult<IReadOnlyDictionary<long, string>>(result);
    }

    public Task EnsureFriendshipExistsForUserPairAsync(long otherUserId, CancellationToken cancellationToken = default)
    {
        var callerId = GetCallerUserId();
        var (low, high) = CanonicalPair(callerId, otherUserId);
        if (!Friendships.Contains((low, high)))
            throw new ArgumentException("Users are not friends");
        return Task.CompletedTask;
    }

    public Task EnsureNoBlockBetweenUsersAsync(long otherUserId, CancellationToken cancellationToken = default)
    {
        var callerId = GetCallerUserId();
        if (Blocks.Contains((callerId, otherUserId)) || Blocks.Contains((otherUserId, callerId)))
            throw new ArgumentException("Cannot chat while a block exists between you and this user.");
        return Task.CompletedTask;
    }

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var callerId = GetCallerUserId();
        foreach (var otherUserId in otherUserIds)
        {
            if (Blocks.Contains((callerId, otherUserId)) || Blocks.Contains((otherUserId, callerId)))
                throw new ArgumentException("Cannot chat while a block exists between you and this user.");
        }

        return Task.CompletedTask;
    }

    public Task EnsureGroupExistsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (!Groups.Contains(groupId))
            throw new EntityNotFoundException("Group not found");
        return Task.CompletedTask;
    }

    public Task EnsureCallerIsGroupMemberAsync(long groupId, CancellationToken cancellationToken = default)
    {
        var callerId = GetCallerUserId();
        return EnsureGroupMemberAsync(groupId, callerId, "Group not found");
    }

    public Task EnsureGroupMembersAsync(
        long groupId,
        IReadOnlyCollection<long> userIds,
        string membersErrorMessage,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            if (!GroupMembers.Contains((groupId, userId)))
                throw new ArgumentException(membersErrorMessage);
        }

        return Task.CompletedTask;
    }

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default)
    {
        if (!GroupMembers.Contains((groupId, userId)))
            throw new EntityNotFoundException(notFoundMessage);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        Friendships.Clear();
        Blocks.Clear();
        Groups.Clear();
        GroupMembers.Clear();
        _nextUserId = 1;
        _nextGroupId = 1;
    }

    private long GetCallerUserId()
    {
        var sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedAccessException("Unauthorized access");
        return long.Parse(sub);
    }

    private static (long Low, long High) CanonicalPair(long userAId, long userBId) =>
        userAId < userBId ? (userAId, userBId) : (userBId, userAId);
}

public sealed record TestUser(long Id, string Nickname);
