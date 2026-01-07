using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IHotelRepository
{
    Task<IEnumerable<Hotel>> GetAllAsync();
    Task<Hotel?> GetByIdAsync(int id);
    Task<Hotel> CreateAsync(Hotel hotel);
    Task<Hotel> UpdateAsync(Hotel hotel);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
