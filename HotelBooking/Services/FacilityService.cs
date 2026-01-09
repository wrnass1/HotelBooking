using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;

namespace HotelBooking.Services;

public class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilityRepository;
    private readonly IHotelRepository _hotelRepository;
    private readonly HotelBookingDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<FacilityService> _logger;

    public FacilityService(
        IFacilityRepository facilityRepository,
        IHotelRepository hotelRepository,
        HotelBookingDbContext context,
        IMapper mapper,
        ILogger<FacilityService> logger)
    {
        _facilityRepository = facilityRepository;
        _hotelRepository = hotelRepository;
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<FacilityDto>> GetAllFacilitiesAsync()
    {
        var facilities = await _facilityRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<FacilityDto>>(facilities);
    }

    public async Task<FacilityDto?> GetFacilityByIdAsync(int id)
    {
        var facility = await _facilityRepository.GetByIdAsync(id);
        return facility == null ? null : _mapper.Map<FacilityDto>(facility);
    }

    public async Task<FacilityDto> CreateFacilityAsync(CreateFacilityDto createFacilityDto)
    {
        var facility = _mapper.Map<Facility>(createFacilityDto);
        var createdFacility = await _facilityRepository.CreateAsync(facility);
        return _mapper.Map<FacilityDto>(createdFacility);
    }

    public async Task<FacilityDto?> UpdateFacilityAsync(int id, UpdateFacilityDto updateFacilityDto)
    {
        var facility = await _facilityRepository.GetByIdAsync(id);
        if (facility == null)
            return null;

        if (!string.IsNullOrEmpty(updateFacilityDto.Name))
            facility.Name = updateFacilityDto.Name;
        if (!string.IsNullOrEmpty(updateFacilityDto.Description))
            facility.Description = updateFacilityDto.Description;
        if (!string.IsNullOrEmpty(updateFacilityDto.Icon))
            facility.Icon = updateFacilityDto.Icon;

        var updatedFacility = await _facilityRepository.UpdateAsync(facility);
        return _mapper.Map<FacilityDto>(updatedFacility);
    }

    public async Task<bool> DeleteFacilityAsync(int id)
    {
        return await _facilityRepository.DeleteAsync(id);
    }

    public async Task<bool> AddFacilityToHotelAsync(int hotelId, int facilityId)
    {
        if (!await _hotelRepository.ExistsAsync(hotelId))
            throw new ArgumentException("Hotel not found", nameof(hotelId));

        if (!await _facilityRepository.ExistsAsync(facilityId))
            throw new ArgumentException("Facility not found", nameof(facilityId));

        var exists = await _context.HotelFacilities
            .AnyAsync(hf => hf.HotelId == hotelId && hf.FacilityId == facilityId);

        if (exists)
            return false;

        var hotelFacility = new HotelFacility
        {
            HotelId = hotelId,
            FacilityId = facilityId
        };

        _context.HotelFacilities.Add(hotelFacility);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFacilityFromHotelAsync(int hotelId, int facilityId)
    {
        var hotelFacility = await _context.HotelFacilities
            .FirstOrDefaultAsync(hf => hf.HotelId == hotelId && hf.FacilityId == facilityId);

        if (hotelFacility == null)
            return false;

        _context.HotelFacilities.Remove(hotelFacility);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<FacilityDto>> GetHotelFacilitiesAsync(int hotelId)
    {
        var facilities = await _context.HotelFacilities
            .Where(hf => hf.HotelId == hotelId)
            .Include(hf => hf.Facility)
            .Select(hf => hf.Facility)
            .ToListAsync();

        return _mapper.Map<IEnumerable<FacilityDto>>(facilities);
    }
}
