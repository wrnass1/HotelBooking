using System.Security.Cryptography;
using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        IApiKeyRepository apiKeyRepository,
        IMapper mapper,
        ILogger<ApiKeyService> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<ApiKeyDto>> GetAllApiKeysAsync()
    {
        var apiKeys = await _apiKeyRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<ApiKeyDto>>(apiKeys);
    }

    public async Task<ApiKeyDto?> GetApiKeyByIdAsync(int id)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(id);
        return apiKey == null ? null : _mapper.Map<ApiKeyDto>(apiKey);
    }

    public async Task<ApiKeyDto> CreateApiKeyAsync(CreateApiKeyDto createApiKeyDto)
    {
        var key = await GenerateApiKeyAsync();
        var apiKey = new ApiKey
        {
            Key = key,
            Name = createApiKeyDto.Name,
            Description = createApiKeyDto.Description,
            IsActive = true,
            ExpiresAt = createApiKeyDto.ExpiresAt
        };

        var createdApiKey = await _apiKeyRepository.CreateAsync(apiKey);
        return _mapper.Map<ApiKeyDto>(createdApiKey);
    }

    public async Task<ApiKeyDto?> UpdateApiKeyAsync(int id, ApiKeyDto updateApiKeyDto)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(id);
        if (apiKey == null)
            return null;

        if (!string.IsNullOrEmpty(updateApiKeyDto.Name))
            apiKey.Name = updateApiKeyDto.Name;
        if (!string.IsNullOrEmpty(updateApiKeyDto.Description))
            apiKey.Description = updateApiKeyDto.Description;
        apiKey.IsActive = updateApiKeyDto.IsActive;
        apiKey.ExpiresAt = updateApiKeyDto.ExpiresAt;

        var updatedApiKey = await _apiKeyRepository.UpdateAsync(apiKey);
        return _mapper.Map<ApiKeyDto>(updatedApiKey);
    }

    public async Task<bool> DeleteApiKeyAsync(int id)
    {
        return await _apiKeyRepository.DeleteAsync(id);
    }

    public async Task<string> GenerateApiKeyAsync()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var key = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return $"hb_{key}";
    }
}
