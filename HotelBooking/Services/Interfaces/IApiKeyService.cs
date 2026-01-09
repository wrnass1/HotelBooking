using HotelBooking.Models.DTO;

namespace HotelBooking.Services.Interfaces;

public interface IApiKeyService
{
    Task<IEnumerable<ApiKeyDto>> GetAllApiKeysAsync();
    Task<ApiKeyDto?> GetApiKeyByIdAsync(int id);
    Task<ApiKeyDto> CreateApiKeyAsync(CreateApiKeyDto createApiKeyDto);
    Task<ApiKeyDto?> UpdateApiKeyAsync(int id, ApiKeyDto updateApiKeyDto);
    Task<bool> DeleteApiKeyAsync(int id);
    Task<string> GenerateApiKeyAsync();
}
