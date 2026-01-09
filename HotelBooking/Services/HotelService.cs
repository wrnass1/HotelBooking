using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace HotelBooking.Services;

public class HotelService : IHotelService
{
    private readonly IHotelRepository _hotelRepository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<HotelService> _logger;

    public HotelService(
        IHotelRepository hotelRepository,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<HotelService> logger)
    {
        _hotelRepository = hotelRepository;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsAsync()
    {
        var hotels = await _hotelRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }

    public async Task<PagedResult<HotelDto>> GetHotelsPagedAsync(HotelQueryDto query)
    {
        var cacheKey = $"hotels_paged_{query.Page}_{query.PageSize}_{query.Search}_{query.City}_{query.Country}_{query.MinStarRating}";
        var cachedResult = await _cacheService.GetAsync<PagedResult<HotelDto>>(cacheKey);
        
        if (cachedResult != null)
        {
            _logger.LogInformation("Returning cached hotels result for page {Page}", query.Page);
            return cachedResult;
        }

        var result = await _hotelRepository.GetPagedAsync(query);
        var dtoResult = new PagedResult<HotelDto>
        {
            Items = _mapper.Map<IEnumerable<HotelDto>>(result.Items),
            Total = result.Total,
            Page = result.Page,
            PageSize = result.PageSize
        };
        
        await _cacheService.SetAsync(cacheKey, dtoResult, TimeSpan.FromMinutes(5));
        
        return dtoResult;
    }

    public async Task<HotelDto?> GetHotelByIdAsync(int id)
    {
        var cacheKey = $"hotel_{id}";
        var cachedHotel = await _cacheService.GetAsync<HotelDto>(cacheKey);
        
        if (cachedHotel != null)
        {
            _logger.LogInformation("Returning cached hotel {HotelId}", id);
            return cachedHotel;
        }

        var hotel = await _hotelRepository.GetByIdAsync(id);
        if (hotel == null)
            return null;

        var hotelDto = _mapper.Map<HotelDto>(hotel);
        await _cacheService.SetAsync(cacheKey, hotelDto, TimeSpan.FromMinutes(10));
        
        return hotelDto;
    }

    public async Task<HotelDto> CreateHotelAsync(CreateHotelDto createHotelDto)
    {
        var hotel = _mapper.Map<Hotel>(createHotelDto);
        var createdHotel = await _hotelRepository.CreateAsync(hotel);
        
        // Invalidate cache
        await _cacheService.RemoveByPatternAsync("hotels_paged_*");
        
        return _mapper.Map<HotelDto>(createdHotel);
    }

    public async Task<HotelDto?> UpdateHotelAsync(int id, UpdateHotelDto updateHotelDto)
    {
        var hotel = await _hotelRepository.GetByIdAsync(id);
        if (hotel == null)
            return null;

        if (!string.IsNullOrEmpty(updateHotelDto.Name))
            hotel.Name = updateHotelDto.Name;
        if (!string.IsNullOrEmpty(updateHotelDto.Address))
            hotel.Address = updateHotelDto.Address;
        if (!string.IsNullOrEmpty(updateHotelDto.City))
            hotel.City = updateHotelDto.City;
        if (!string.IsNullOrEmpty(updateHotelDto.Country))
            hotel.Country = updateHotelDto.Country;
        if (!string.IsNullOrEmpty(updateHotelDto.Description))
            hotel.Description = updateHotelDto.Description;
        if (updateHotelDto.StarRating.HasValue)
            hotel.StarRating = updateHotelDto.StarRating.Value;

        var updatedHotel = await _hotelRepository.UpdateAsync(hotel);
        
        // Invalidate cache
        await _cacheService.RemoveByPatternAsync("hotels_paged_*");
        await _cacheService.RemoveAsync($"hotel_{id}");
        
        return _mapper.Map<HotelDto>(updatedHotel);
    }

    public async Task<bool> DeleteHotelAsync(int id)
    {
        var result = await _hotelRepository.DeleteAsync(id);
        
        if (result)
        {
            // Invalidate cache
            await _cacheService.RemoveByPatternAsync("hotels_paged_*");
            await _cacheService.RemoveAsync($"hotel_{id}");
        }
        
        return result;
    }
}
