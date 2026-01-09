using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;

namespace HotelBooking.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly HotelBookingDbContext _context;

    public FacilityRepository(HotelBookingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Facility>> GetAllAsync()
    {
        return await _context.Facilities.ToListAsync();
    }

    public async Task<Facility?> GetByIdAsync(int id)
    {
        return await _context.Facilities.FindAsync(id);
    }

    public async Task<Facility> CreateAsync(Facility facility)
    {
        facility.CreatedAt = DateTime.UtcNow;
        _context.Facilities.Add(facility);
        await _context.SaveChangesAsync();
        return facility;
    }

    public async Task<Facility> UpdateAsync(Facility facility)
    {
        _context.Facilities.Update(facility);
        await _context.SaveChangesAsync();
        return facility;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var facility = await _context.Facilities.FindAsync(id);
        if (facility == null)
            return false;

        _context.Facilities.Remove(facility);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Facilities.AnyAsync(f => f.Id == id);
    }
}
