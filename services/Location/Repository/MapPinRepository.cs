using Location.Entities;
using Location.Db;
using Location.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Location.Repository;

[Repository]
public class MapPinRepository(LocationDbContext context) : IMapPinRepository
{
    private readonly LocationDbContext _context = context;

    public Task CreateMapPinAsync(MapPin pin)
    {
        _context.MapPins.Add(pin);
        return _context.SaveChangesAsync();
    }

    public Task<MapPin?> GetMapPinByIdAsync(long id) =>
        _context.MapPins.FindAsync(id).AsTask();

    public Task<bool> ExistsMapPinByIdAsync(long id) =>
        _context.MapPins.AnyAsync(p => p.Id == id);

    public Task<List<MapPin>> GetMapPinsInBoundsAsync(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude) =>
        _context.MapPins
            .Where(p =>
                p.Latitude >= minLatitude &&
                p.Latitude <= maxLatitude &&
                p.Longitude >= minLongitude &&
                p.Longitude <= maxLongitude)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public Task<MapPin?> GetMapPinByPostIdAsync(long postId) =>
        _context.MapPins.FirstOrDefaultAsync(p => p.PostId == postId);

    public Task<List<MapPin>> GetMapPinsByPostIdsAsync(IEnumerable<long> postIds) =>
        _context.MapPins
            .Where(p => p.PostId != null && postIds.Contains(p.PostId.Value))
            .ToListAsync();

    public Task UpdateMapPinAsync(MapPin pin) => _context.SaveChangesAsync();

    public Task DeleteMapPinAsync(MapPin pin)
    {
        _context.MapPins.Remove(pin);
        return _context.SaveChangesAsync();
    }

    public Task DeleteAllByUserIdAsync(long userId) =>
        _context.MapPins.Where(p => p.UserId == userId).ExecuteDeleteAsync();
}
