using Api.Domain.Location.Domain;
using Api.Domain.Posts.Service;
using Api.Domain.UserBlocks.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Location.Service;

[Service]
public class LocationAccessService(
    PostService postService,
    UserBlockService userBlockService,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly PostService _postService = postService;
    private readonly UserBlockService _userBlockService = userBlockService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public long? TryGetViewerUserId()
    {
        var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
        return long.TryParse(sub, out var id) ? id : null;
    }

    public async Task EnsureCanCreateMapPinAsync(long userId, long? postId)
    {
        if (!postId.HasValue) return;

        var post = await _postService.GetPostByIdAsync(postId.Value)
            ?? throw new EntityNotFoundException("Post not found", StatusCodes.Status400BadRequest);

        if (post.AuthorId != userId) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public void EnsureCanDeleteMapPin(MapPin pin, long userId)
    {
        if (pin.OwnerUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public async Task<bool> CanViewMapPinAsync(MapPin pin, long? viewerUserId)
    {
        if (viewerUserId == pin.OwnerUserId) return true;

        if (viewerUserId is not null &&
            await _userBlockService.AnyBlockExistsBetweenUserAndOthersAsync(viewerUserId.Value, [pin.OwnerUserId]))
            return false;

        if (pin.PostId is null) return false;

        return await _postService.TryCanViewPostAsync(pin.PostId.Value);
    }

    public async Task<List<MapPin>> FilterViewableMapPinsAsync(IReadOnlyList<MapPin> pins, long? viewerUserId)
    {
        if (pins.Count == 0) return [];

        var ownerIds = pins.Select(p => p.OwnerUserId).Distinct().ToList();
        var blockedOwnerIds = viewerUserId is null
            ? []
            : await _userBlockService.GetMutuallyBlockedUserIdsAsync(viewerUserId.Value, ownerIds);

        var postIds = pins.Where(p => p.PostId is not null).Select(p => p.PostId!.Value).Distinct().ToList();
        var viewablePostIds = postIds.Count == 0
            ? []
            : await _postService.GetViewablePostIdsAsync(postIds, viewerUserId);

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
