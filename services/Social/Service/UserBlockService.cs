using Social.Client;
using Social.Exceptions;
using Social.Service;
using Social.Infrastructure;
using Social.Entities;
using Social.Dto;
using Social.Repository;

namespace Social.Service;

[Service]
public class UserBlockService(
    IUserBlockRepository repo,
    Lazy<FriendRequestService> friendRequestService,
    IUserClient userClient,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IUserBlockRepository _repo = repo;
    private readonly Lazy<FriendRequestService> _friendRequestService = friendRequestService;
    private readonly IUserClient _userClient = userClient;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    private async Task<UserBlock> GetBlockOrThrowAsync(long id) =>
        await _repo.GetUserBlockByIdAsync(id) ?? throw new EntityNotFoundException("Block not found");

    public async Task BlockUserAsync(UserBlockCreateRequestDto request)
    {
        var blockerId = GetUserIdFromLogin();
        await ValidateBlockPartiesAsync(blockerId, request.BlockedUserId);

        if (await _repo.ExistsUserBlockAsync(blockerId, request.BlockedUserId))
            throw new EntityAlreadyExistsException($"User {request.BlockedUserId} is already blocked.");

        await _repo.CreateUserBlockAsync(new UserBlock(blockerId, request.BlockedUserId));
        await _friendRequestService.Value.HandlePendingFriendRequestOnBlockAsync(blockerId, request.BlockedUserId);
    }

    public async Task<List<UserBlockGetResponseDto>?> GetMyBlocksAsync()
    {
        var blockerId = GetUserIdFromLogin();
        var blocks = await _repo.GetAllForBlockerAsync(blockerId);
        if (blocks.Count == 0) return null;
        return await MapManyAsync(blocks);
    }

    public async Task DeleteBlockByIdAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        var block = await GetBlockOrThrowAsync(id);
        if (block.BlockerId != userId) throw new UnauthorizedAccessException("Unauthorized access");

        var blockedUserId = block.BlockedUserId;
        await _repo.DeleteUserBlockAsync(block);
        await _friendRequestService.Value.HandlePendingFriendRequestOnUnblockAsync(userId, blockedUserId);
    }

    public Task<bool> IsBlockedByAsync(long blockerId, long blockedUserId) =>
        _repo.ExistsUserBlockAsync(blockerId, blockedUserId);

    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(long userId, IReadOnlyCollection<long> otherUserIds) =>
        _repo.AnyBlockExistsBetweenUserAndOthersAsync(userId, otherUserIds);

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(long userId, IReadOnlyCollection<long> otherUserIds) =>
        _repo.GetMutuallyBlockedUserIdsAsync(userId, otherUserIds);

    public async Task EnsureNoBlockBetweenUserAndOthersAsync(long userId, IReadOnlyCollection<long> otherUserIds)
    {
        if (await AnyBlockExistsBetweenUserAndOthersAsync(userId, otherUserIds))
            throw new ArgumentException("Cannot chat while a block exists between you and this user.");
    }

    public Task DeleteAllBlocksForUserAsync(long userId) => _repo.DeleteAllForUserAsync(userId);

    private async Task ValidateBlockPartiesAsync(long blockerId, long blockedUserId)
    {
        if (blockerId == blockedUserId) throw new ArgumentException("Cannot block yourself.");
        await _userClient.EnsureUserExistsAsync(blockerId, "Authentication failed", StatusCodes.Status400BadRequest);
        await _userClient.EnsureUserExistsAsync(blockedUserId, "User not found", StatusCodes.Status400BadRequest);
    }

    private static UserBlockGetResponseDto MapToDto(UserBlock block, string blockedUserNickname) =>
        new(
            Id: block.Id,
            BlockedUserId: block.BlockedUserId,
            BlockedUserNickname: blockedUserNickname,
            CreatedAt: block.CreatedAt,
            UpdatedAt: block.UpdatedAt);

    private async Task<List<UserBlockGetResponseDto>> MapManyAsync(IReadOnlyList<UserBlock> blocks)
    {
        var blockedUserIds = blocks.Select(b => b.BlockedUserId).Distinct();
        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(blockedUserIds);

        return [.. blocks.Select(b =>
            MapToDto(b, nicknames.GetValueOrDefault(b.BlockedUserId, "Deleted User")))];
    }
}
