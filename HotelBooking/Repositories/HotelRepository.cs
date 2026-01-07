using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;

namespace HotelBooking.Repositories;

public class HotelRepository : IHotelRepository
{
    private readonly HotelBookingDbContext _context;

    public HotelRepository(HotelBookingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Hotel>> GetAllAsync()
    {
        return await _context.Hotels
            .Include(h => h.Rooms)
            .ToListAsync();
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

        _context.Hotels.Remove(hotel);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Hotels.AnyAsync(h => h.Id == id);
    }
}
