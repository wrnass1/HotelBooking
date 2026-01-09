using HotelBooking.Models.Entities;

namespace HotelBooking.Repositories.Interfaces;

public interface IApiKeyRepository
{
    Task<IEnumerable<ApiKey>> GetAllAsync();
    Task<ApiKey?> GetByIdAsync(int id);
    Task<ApiKey?> GetByKeyAsync(string key);
    Task<ApiKey> CreateAsync(ApiKey apiKey);
    Task<ApiKey> UpdateAsync(ApiKey apiKey);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
