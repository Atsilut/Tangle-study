using Location.Client;
using Location.Entities;
using Location.Exceptions;
using Location.Infrastructure;

namespace Location.Service;

[Service]
public class LocationAccessService(
    IMonolithAccessClient monolithAccess,
    ISocialClient socialClient,
    ICommunityAccessClient communityAccess,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IMonolithAccessClient _monolithAccess = monolithAccess;
    private readonly ISocialClient _socialClient = socialClient;
    private readonly ICommunityAccessClient _communityAccess = communityAccess;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public long? TryGetViewerUserId()
    {
        var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        return long.TryParse(sub, out var id) ? id : null;
    }

    public async Task EnsureCanCreateMapPinAsync(long userId, long? postId)
    {
        if (!postId.HasValue) return;

        try
        {
            await _communityAccess.EnsurePostOwnerAsync(postId.Value);
        }
        catch (EntityNotFoundException)
        {
            throw new EntityNotFoundException("Post not found", StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Unauthorized access");
        }
    }

    public void EnsureCanDeleteMapPin(MapPin pin, long userId)
    {
        if (pin.OwnerUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public async Task<bool> CanViewMapPinAsync(MapPin pin, long? viewerUserId)
    {
        if (viewerUserId == pin.OwnerUserId) return true;

        if (viewerUserId is not null &&
            await _socialClient.AnyBlockExistsBetweenUserAndOthersAsync(viewerUserId.Value, [pin.OwnerUserId]))
            return false;

        if (pin.PostId is null) return false;

        var viewable = await _communityAccess.GetViewablePostIdsAsync([pin.PostId.Value], viewerUserId);
        return viewable.Contains(pin.PostId.Value);
    }

    public async Task<List<MapPin>> FilterViewableMapPinsAsync(IReadOnlyList<MapPin> pins, long? viewerUserId)
    {
        if (pins.Count == 0) return [];

        var ownerIds = pins.Select(p => p.OwnerUserId).Distinct().ToList();
        var blockedOwnerIds = viewerUserId is null
            ? []
            : await _socialClient.GetMutuallyBlockedUserIdsAsync(viewerUserId.Value, ownerIds);

        var postIds = pins.Where(p => p.PostId is not null).Select(p => p.PostId!.Value).Distinct().ToList();
        var viewablePostIds = postIds.Count == 0
            ? []
            : await _communityAccess.GetViewablePostIdsAsync(postIds, viewerUserId);

        List<MapPin> visible = [];
        foreach (var pin in pins)
        {
            if (viewerUserId == pin.OwnerUserId)
            {
                visible.Add(pin);
                continue;
            }

            if (blockedOwnerIds.Contains(pin.OwnerUserId)) continue;

            if (pin.PostId is not null && viewablePostIds.Contains(pin.PostId.Value)) visible.Add(pin);
        }

        return visible;
    }
}
