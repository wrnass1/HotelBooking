using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IBookingRepository
{
    Task<IEnumerable<Booking>> GetAllAsync();
    Task<IEnumerable<Booking>> GetByRoomIdAsync(int roomId);
    Task<IEnumerable<Booking>> GetByGuestEmailAsync(string email);
    Task<Booking?> GetByIdAsync(int id);
    Task<Booking> CreateAsync(Booking booking);
    Task<Booking> UpdateAsync(Booking booking);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);
}
