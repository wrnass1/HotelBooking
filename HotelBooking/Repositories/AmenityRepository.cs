using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;

namespace HotelBooking.Repositories;

public class AmenityRepository : IAmenityRepository
{
    private readonly HotelBookingDbContext _context;

    public AmenityRepository(HotelBookingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Amenity>> GetAllAsync()
    {
        return await _context.Amenities.ToListAsync();
    }

    public async Task<Amenity?> GetByIdAsync(int id)
    {
        return await _context.Amenities.FindAsync(id);
    }

    public async Task<Amenity> CreateAsync(Amenity amenity)
    {
        amenity.CreatedAt = DateTime.UtcNow;
        _context.Amenities.Add(amenity);
        await _context.SaveChangesAsync();
        return amenity;
    }

    public async Task<Amenity> UpdateAsync(Amenity amenity)
    {
        _context.Amenities.Update(amenity);
        await _context.SaveChangesAsync();
        return amenity;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var amenity = await _context.Amenities.FindAsync(id);
        if (amenity == null)
            return false;

        _context.Amenities.Remove(amenity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Amenities.AnyAsync(a => a.Id == id);
    }
}
