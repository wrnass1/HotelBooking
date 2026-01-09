using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;

namespace HotelBooking.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly HotelBookingDbContext _context;

    public RoomRepository(HotelBookingDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        return await _context.Rooms
            .Include(r => r.Hotel)
            .ToListAsync();
    }

    public async Task<IEnumerable<Room>> GetByHotelIdAsync(int hotelId)
    {
        return await _context.Rooms
            .Include(r => r.Hotel)
            .Where(r => r.HotelId == hotelId)
            .ToListAsync();
    }

    public async Task<Room?> GetByIdAsync(int id)
    {
        return await _context.Rooms
            .Include(r => r.Hotel)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Room> CreateAsync(Room room)
    {
        room.CreatedAt = DateTime.UtcNow;
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task<Room> UpdateAsync(Room room)
    {
        room.UpdatedAt = DateTime.UtcNow;
        _context.Rooms.Update(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null)
            return false;

        _context.Rooms.Remove(room);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Rooms.AnyAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Room>> GetAvailableRoomsAsync(int hotelId, DateTime checkIn, DateTime checkOut)
    {
        var bookedRoomIds = await _context.Bookings
            .Where(b => b.Status != "Cancelled" &&
                       ((b.CheckInDate <= checkIn && b.CheckOutDate > checkIn) ||
                        (b.CheckInDate < checkOut && b.CheckOutDate >= checkOut) ||
                        (b.CheckInDate >= checkIn && b.CheckOutDate <= checkOut)))
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync();

        return await _context.Rooms
            .Include(r => r.Hotel)
            .Where(r => r.HotelId == hotelId &&
                       r.IsAvailable &&
                       !bookedRoomIds.Contains(r.Id))
            .ToListAsync();
    }
}
