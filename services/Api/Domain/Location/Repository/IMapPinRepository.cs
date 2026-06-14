using Api.Domain.Location.Domain;

namespace Api.Domain.Location.Repository;

public interface IMapPinRepository
{
    public Task CreateMapPinAsync(MapPin pin);
    public Task<MapPin?> GetMapPinByIdAsync(long id);
    public Task<bool> ExistsMapPinByIdAsync(long id);
    public Task<List<MapPin>> GetMapPinsInBoundsAsync(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude);
    public Task DeleteMapPinAsync(MapPin pin);
    public Task DeleteAllByUserIdAsync(long userId);
}
