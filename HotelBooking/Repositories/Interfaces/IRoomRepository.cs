using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<IEnumerable<Room>> GetByHotelIdAsync(int hotelId);
    Task<Room?> GetByIdAsync(int id);
    Task<Room> CreateAsync(Room room);
    Task<Room> UpdateAsync(Room room);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<IEnumerable<Room>> GetAvailableRoomsAsync(int hotelId, DateTime checkIn, DateTime checkOut);
}
