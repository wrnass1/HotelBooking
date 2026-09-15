using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Sharding;

namespace HotelBooking.Repositories;

public class HotelRepository : IHotelRepository
{
    private readonly HotelBookingDbContext _context;
    private readonly HotelBookingReadDbContext _readContext;
    private readonly BookingShardStore? _bookingShards;

    public HotelRepository(HotelBookingDbContext context, HotelBookingReadDbContext readContext,
        BookingShardStore? bookingShards = null)
    {
        _context = context;
        _readContext = readContext;
        _bookingShards = bookingShards;
    }

    public async Task<IEnumerable<Hotel>> GetAllAsync()
    {
        return await _context.Hotels
            .Include(h => h.Rooms)
            .ToListAsync();
    }

    public async Task<PagedResult<Hotel>> GetPagedAsync(HotelQueryDto query)
    {
        // The catalog tolerates replication lag. Writes and point reads use Primary.
        var queryable = _readContext.Hotels.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrEmpty(query.Search))
        {
            queryable = queryable.Where(h => 
                h.Name.Contains(query.Search) || 
                h.Description != null && h.Description.Contains(query.Search));
        }

        if (!string.IsNullOrEmpty(query.City))
        {
            queryable = queryable.Where(h => h.City == query.City);
        }

        if (!string.IsNullOrEmpty(query.Country))
        {
            queryable = queryable.Where(h => h.Country == query.Country);
        }

        if (query.MinStarRating.HasValue)
        {
            queryable = queryable.Where(h => h.StarRating >= query.MinStarRating.Value);
        }

        var total = await queryable.CountAsync();

        var hotels = await queryable
            .Include(h => h.Rooms)
            .OrderBy(h => h.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<Hotel>
        {
            Items = hotels,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<Hotel?> GetByIdAsync(int id)
    {
        return await _context.Hotels
            .Include(h => h.Rooms)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<Hotel> CreateAsync(Hotel hotel)
    {
        hotel.CreatedAt = DateTime.UtcNow;
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();
        return hotel;
    }

    public async Task<Hotel> UpdateAsync(Hotel hotel)
    {
        hotel.UpdatedAt = DateTime.UtcNow;
        _context.Hotels.Update(hotel);
        await _context.SaveChangesAsync();
        return hotel;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var hotel = await _context.Hotels.FindAsync(id);
        if (hotel == null)
            return false;

        if (_bookingShards != null)
        {
            var roomIds = await _context.Rooms.Where(r => r.HotelId == id).Select(r => r.Id).ToArrayAsync();
            if (await _bookingShards.AnyAsync("\"RoomId\"=ANY(@RoomIds)", new { RoomIds = roomIds }))
                throw new InvalidOperationException("Cannot delete a hotel with bookings on shards.");
        }
        _context.Hotels.Remove(hotel);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Hotels.AnyAsync(h => h.Id == id);
    }
}
