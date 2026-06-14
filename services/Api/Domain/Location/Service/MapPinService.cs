using Api.Domain.Location.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Location.Repository;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Location.Service;

[Service]
public class MapPinService(
    IMapPinRepository repo,
    UserService userService,
    LocationAccessService access,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IMapPinRepository _repo = repo;
    private readonly UserService _userService = userService;
    private readonly LocationAccessService _access = access;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<MapPinGetResponseDto> CreateMapPinAsync(MapPinCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);
        ValidateBounds(request.Latitude, request.Longitude);
        await _access.EnsureCanCreateMapPinAsync(userId, request.PostId);

        var pin = new MapPin(userId, request.Latitude, request.Longitude, request.PostId);
        await _repo.CreateMapPinAsync(pin);

        var nickname = (await _userService.GetNicknamesByUserIdsAsync([userId])).GetValueOrDefault(userId, "Deleted User");
        return MapToDto(pin, nickname);
    }

    public async Task<MapPinGetResponseDto?> GetMapPinByIdAsync(long id)
    {
        var pin = await _repo.GetMapPinByIdAsync(id);
        if (pin is null) return null;

        var viewerUserId = _access.TryGetViewerUserId();
        if (!await _access.CanViewMapPinAsync(pin, viewerUserId)) return null;

        var nickname = (await _userService.GetNicknamesByUserIdsAsync([pin.OwnerUserId]))
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
    }

    public Task HandleUserDeletionAsync(long userId) => _repo.DeleteAllByUserIdAsync(userId);

    private async Task<List<MapPinGetResponseDto>> MapManyAsync(IReadOnlyList<MapPin> pins)
    {
        var nicknames = await _userService.GetNicknamesByUserIdsAsync(pins.Select(p => p.OwnerUserId));
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
