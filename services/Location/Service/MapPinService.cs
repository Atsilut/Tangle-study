using Location.Client;
using Location.Entities;
using Location.Dto;
using Location.Repository;
using Location.Exceptions;
using Location.Infrastructure;

namespace Location.Service;

[Service]
public class MapPinService(
    IMapPinRepository repo,
    IMonolithAccessClient monolithAccess,
    LocationAccessService access,
    Lazy<LocationClusterService> clusterService,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IMapPinRepository _repo = repo;
    private readonly IMonolithAccessClient _monolithAccess = monolithAccess;
    private readonly LocationAccessService _access = access;
    private readonly Lazy<LocationClusterService> _clusterService = clusterService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<MapPinGetResponseDto> CreateMapPinAsync(MapPinCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _monolithAccess.EnsureUserExistsAsync(userId);
        ValidateBounds(request.Latitude, request.Longitude);
        await _access.EnsureCanCreateMapPinAsync(userId, request.PostId);

        var pin = new MapPin(userId, request.Latitude, request.Longitude, request.PostId);
        await _repo.CreateMapPinAsync(pin);
        await _clusterService.Value.RefreshClustersNearPinAsync(pin.Latitude, pin.Longitude);

        var nickname = (await _monolithAccess.GetNicknamesByUserIdsAsync([userId])).GetValueOrDefault(userId, "Deleted User");
        return MapToDto(pin, nickname);
    }

    public async Task<MapPinGetResponseDto?> GetMapPinByIdAsync(long id)
    {
        var pin = await _repo.GetMapPinByIdAsync(id);
        if (pin is null) return null;

        var viewerUserId = _access.TryGetViewerUserId();
        if (!await _access.CanViewMapPinAsync(pin, viewerUserId)) return null;

        var nickname = (await _monolithAccess.GetNicknamesByUserIdsAsync([pin.OwnerUserId]))
            .GetValueOrDefault(pin.OwnerUserId, "Deleted User");
        return MapToDto(pin, nickname);
    }

    public async Task<List<MapPinGetResponseDto>?> GetMapPinsInBoundsAsync(MapPinBoundsQueryDto query)
    {
        ValidateBoundsQuery(query);

        var pins = await _repo.GetMapPinsInBoundsAsync(
            query.MinLatitude,
            query.MaxLatitude,
            query.MinLongitude,
            query.MaxLongitude);
        if (pins.Count == 0) return null;

        var viewerUserId = _access.TryGetViewerUserId();
        var visible = await _access.FilterViewableMapPinsAsync(pins, viewerUserId);
        if (visible.Count == 0) return null;

        return await MapManyAsync(visible);
    }

    public async Task DeleteMapPinByIdAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        var pin = await _repo.GetMapPinByIdAsync(id) ?? throw new EntityNotFoundException("Map pin not found");
        _access.EnsureCanDeleteMapPin(pin, userId);
        await _repo.DeleteMapPinAsync(pin);
        await _clusterService.Value.RefreshClustersNearPinAsync(pin.Latitude, pin.Longitude);
    }

    public Task HandleUserDeletionAsync(long userId) => _repo.DeleteAllByUserIdAsync(userId);

    public async Task UpsertLocationForPostAsync(long postId, long userId, decimal latitude, decimal longitude)
    {
        ValidateBounds(latitude, longitude);
        var existing = await _repo.GetMapPinByPostIdAsync(postId);
        if (existing is not null)
        {
            if (existing.OwnerUserId != userId) throw new UnauthorizedAccessException();
            existing.UpdateCoordinates(latitude, longitude);
            await _repo.UpdateMapPinAsync(existing);
            await _clusterService.Value.RefreshClustersNearPinAsync(latitude, longitude);
            return;
        }

        await _repo.CreateMapPinAsync(new MapPin(userId, latitude, longitude, postId));
        await _clusterService.Value.RefreshClustersNearPinAsync(latitude, longitude);
    }

    public async Task ClearLocationForPostAsync(long postId, long userId)
    {
        var pin = await _repo.GetMapPinByPostIdAsync(postId);
        if (pin is null) return;
        if (pin.OwnerUserId != userId) throw new UnauthorizedAccessException();
        await _repo.DeleteMapPinAsync(pin);
        await _clusterService.Value.RefreshClustersNearPinAsync(pin.Latitude, pin.Longitude);
    }

    public async Task ClearLocationForPostOnDeleteAsync(long postId)
    {
        var pin = await _repo.GetMapPinByPostIdAsync(postId);
        if (pin is null) return;
        await _repo.DeleteMapPinAsync(pin);
        await _clusterService.Value.RefreshClustersNearPinAsync(pin.Latitude, pin.Longitude);
    }

    public async Task<IReadOnlyDictionary<long, PostLocationGetResponseDto>> GetLocationsByPostIdsAsync(
        IEnumerable<long> postIds)
    {
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, PostLocationGetResponseDto>();

        var pins = await _repo.GetMapPinsByPostIdsAsync(ids);
        return pins
            .Where(p => p.PostId.HasValue)
            .ToDictionary(
                p => p.PostId!.Value,
                p => new PostLocationGetResponseDto(p.Latitude, p.Longitude));
    }

    private async Task<List<MapPinGetResponseDto>> MapManyAsync(IReadOnlyList<MapPin> pins)
    {
        var nicknames = await _monolithAccess.GetNicknamesByUserIdsAsync(pins.Select(p => p.OwnerUserId));
        return [.. pins.Select(pin => MapToDto(
            pin,
            nicknames.GetValueOrDefault(pin.OwnerUserId, "Deleted User")))];
    }

    private static MapPinGetResponseDto MapToDto(MapPin pin, string ownerNickname) =>
        new(
            Id: pin.Id,
            Latitude: pin.Latitude,
            Longitude: pin.Longitude,
            OwnerUserId: pin.OwnerUserId,
            OwnerNickname: ownerNickname,
            PostId: pin.PostId,
            CreatedAt: pin.CreatedAt,
            UpdatedAt: pin.UpdatedAt);

    private static void ValidateBounds(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90) throw new ArgumentException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new ArgumentException("Longitude must be between -180 and 180.");
    }

    private static void ValidateBoundsQuery(MapPinBoundsQueryDto query)
    {
        ValidateBounds(query.MinLatitude, query.MinLongitude);
        ValidateBounds(query.MaxLatitude, query.MaxLongitude);

        if (query.MinLatitude > query.MaxLatitude)
            throw new ArgumentException("MinLatitude must be less than or equal to MaxLatitude.");

        if (query.MinLongitude > query.MaxLongitude)
            throw new ArgumentException("MinLongitude must be less than or equal to MaxLongitude.");
    }
}
