using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IAmenityRepository
{
    Task<IEnumerable<Amenity>> GetAllAsync();
    Task<Amenity?> GetByIdAsync(int id);
    Task<Amenity> CreateAsync(Amenity amenity);
    Task<Amenity> UpdateAsync(Amenity amenity);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
