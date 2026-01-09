using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IFacilityRepository
{
    Task<IEnumerable<Facility>> GetAllAsync();
    Task<Facility?> GetByIdAsync(int id);
    Task<Facility> CreateAsync(Facility facility);
    Task<Facility> UpdateAsync(Facility facility);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
