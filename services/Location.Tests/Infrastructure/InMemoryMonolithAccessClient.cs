using System.Security.Claims;
using Location.Client;
using Location.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Location.Tests.Infrastructure;

/// <summary>
/// Configurable in-memory monolith access checks for Location integration and unit tests.
/// </summary>
public sealed class InMemoryMonolithAccessClient(IHttpContextAccessor httpContextAccessor)
    : IMonolithAccessClient, ISocialClient, ICommunityAccessClient, IGroupClient
{
    private long _nextUserId = 1;
    private long _nextGroupId = 1;
    private long _nextPostId = 1;

    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public HashSet<(long Blocker, long Blocked)> Blocks { get; } = [];
    public HashSet<long> Groups { get; } = [];
    public HashSet<(long GroupId, long UserId)> GroupMembers { get; } = [];
    public Dictionary<long, long> PostOwners { get; } = [];
    public HashSet<(long PostId, long ViewerUserId)> ViewablePosts { get; } = [];

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

    public void AddGroupMember(long groupId, long userId)
    {
        Groups.Add(groupId);
        Users.Add(userId);
        GroupMembers.Add((groupId, userId));
    }

    public void AddBlock(long blockerUserId, long blockedUserId) =>
        Blocks.Add((blockerUserId, blockedUserId));

    public long SeedPost(long ownerUserId, long? postId = null)
    {
        Users.Add(ownerUserId);
        var id = postId ?? _nextPostId++;
        PostOwners[id] = ownerUserId;
        return id;
    }

    public void SetPostViewable(long postId, long viewerUserId) =>
        ViewablePosts.Add((postId, viewerUserId));

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new ArgumentException("User not found");
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

    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default)
    {
        if (!PostOwners.TryGetValue(postId, out var ownerId))
            throw new EntityNotFoundException("Post not found");

        var callerId = GetCallerUserId();
        if (callerId != ownerId)
            throw new UnauthorizedAccessException("Unauthorized access");

        return Task.CompletedTask;
    }

    public Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewable = new HashSet<long>();
        foreach (var postId in postIds)
        {
            if (!PostOwners.ContainsKey(postId)) continue;

            if (viewerUserId is null)
            {
                viewable.Add(postId);
                continue;
            }

            if (PostOwners[postId] == viewerUserId || ViewablePosts.Contains((postId, viewerUserId.Value)))
                viewable.Add(postId);
        }

        return Task.FromResult(viewable);
    }

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var blocked = new HashSet<long>();
        foreach (var otherUserId in otherUserIds)
        {
            if (Blocks.Contains((userId, otherUserId)) || Blocks.Contains((otherUserId, userId)))
                blocked.Add(otherUserId);
        }

        return Task.FromResult(blocked);
    }

    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var otherUserId in otherUserIds)
        {
            if (Blocks.Contains((userId, otherUserId)) || Blocks.Contains((otherUserId, userId)))
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
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

    public Task<bool> IsGroupMemberAsync(long groupId, long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupMembers.Contains((groupId, userId)));

    public Task<IReadOnlyList<GroupMemberSummaryDto>> GetGroupMembersForMemberAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        var members = GroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => new GroupMemberSummaryDto(m.UserId, Nicknames.GetValueOrDefault(m.UserId, "Deleted User")))
            .ToList();
        return Task.FromResult<IReadOnlyList<GroupMemberSummaryDto>>(members);
    }

    public Task<IReadOnlyList<long>> GetGroupMemberUserIdsAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        var memberIds = GroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToList();
        return Task.FromResult<IReadOnlyList<long>>(memberIds);
    }

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        Blocks.Clear();
        Groups.Clear();
        GroupMembers.Clear();
        PostOwners.Clear();
        ViewablePosts.Clear();
        _nextUserId = 1;
        _nextGroupId = 1;
        _nextPostId = 1;
    }

    private long GetCallerUserId()
    {
        var sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedAccessException("Unauthorized access");
        return long.Parse(sub);
    }
}

public sealed record TestUser(long Id, string Nickname);
