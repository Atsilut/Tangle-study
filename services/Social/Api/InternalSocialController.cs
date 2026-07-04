using Microsoft.AspNetCore.Mvc;
using Social.Db;
using Social.Friendships.Service;
using Social.Security;
using Social.UserBlocks.Service;

namespace Social.Api;

[ApiController]
[Route("internal/social")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalSocialController(
    FriendshipService friendshipService,
    FriendRequestService friendRequestService,
    UserBlockService userBlockService,
    SocialDbContext db) : ControllerBase
{
    [HttpPost("friendships/validate-pair")]
    public async Task<IActionResult> ValidateFriendshipPair([FromBody] InternalSocialUserPairRequestDto request)
    {
        await friendshipService.EnsureFriendshipExistsForUserPairAsync(request.UserId, request.OtherUserId);
        return NoContent();
    }

    [HttpPost("blocks/validate-between")]
    public async Task<IActionResult> ValidateNoBlockBetweenUsers([FromBody] InternalSocialUserPairRequestDto request)
    {
        if (await userBlockService.IsBlockedByAsync(request.UserId, request.OtherUserId)
            || await userBlockService.IsBlockedByAsync(request.OtherUserId, request.UserId))
            throw new ArgumentException("Cannot chat while a block exists between you and this user.");

        return NoContent();
    }

    [HttpPost("blocks/validate-against-others")]
    public async Task<IActionResult> ValidateNoBlockAgainstOthers([FromBody] InternalSocialMutualBlocksRequestDto request)
    {
        await userBlockService.EnsureNoBlockBetweenUserAndOthersAsync(request.UserId, request.OtherUserIds);
        return NoContent();
    }

    [HttpPost("blocks/mutual-ids")]
    public async Task<ActionResult<InternalSocialMutualBlocksResponseDto>> GetMutualBlockIds(
        [FromBody] InternalSocialMutualBlocksRequestDto request)
    {
        var blocked = await userBlockService.GetMutuallyBlockedUserIdsAsync(request.UserId, request.OtherUserIds);
        return Ok(new InternalSocialMutualBlocksResponseDto([.. blocked]));
    }

    [HttpPost("blocks/is-blocked-by")]
    public async Task<ActionResult<InternalSocialIsBlockedResponseDto>> IsBlockedBy(
        [FromBody] InternalSocialIsBlockedRequestDto request)
    {
        var blocked = await userBlockService.IsBlockedByAsync(request.BlockerUserId, request.BlockedUserId);
        return Ok(new InternalSocialIsBlockedResponseDto(blocked));
    }

    [HttpPost("users/{userId:long}/detach-on-deletion")]
    public async Task<IActionResult> DetachOnDeletion([FromRoute] long userId)
    {
        await db.ExecuteInTransactionAsync(async () =>
        {
            await friendshipService.DeleteAllFriendshipsForUserAsync(userId);
            await friendRequestService.DeleteAllFriendRequestsForUserAsync(userId);
            await userBlockService.DeleteAllBlocksForUserAsync(userId);
        });
        return NoContent();
    }
}

public sealed record InternalSocialUserPairRequestDto(long UserId, long OtherUserId);

public sealed record InternalSocialMutualBlocksRequestDto(long UserId, long[] OtherUserIds);

public sealed record InternalSocialMutualBlocksResponseDto(long[] BlockedUserIds);

public sealed record InternalSocialIsBlockedRequestDto(long BlockerUserId, long BlockedUserId);

public sealed record InternalSocialIsBlockedResponseDto(bool IsBlocked);
