using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;

namespace HotelBooking.Services;

public class AmenityService : IAmenityService
{
    private readonly IAmenityRepository _amenityRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly HotelBookingDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<AmenityService> _logger;

    public AmenityService(
        IAmenityRepository amenityRepository,
        IRoomRepository roomRepository,
        HotelBookingDbContext context,
        IMapper mapper,
        ILogger<AmenityService> logger)
    {
        _amenityRepository = amenityRepository;
        _roomRepository = roomRepository;
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<AmenityDto>> GetAllAmenitiesAsync()
    {
        var amenities = await _amenityRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<AmenityDto>>(amenities);
    }

    public async Task<AmenityDto?> GetAmenityByIdAsync(int id)
    {
        var amenity = await _amenityRepository.GetByIdAsync(id);
        return amenity == null ? null : _mapper.Map<AmenityDto>(amenity);
    }

    public async Task<AmenityDto> CreateAmenityAsync(CreateAmenityDto createAmenityDto)
    {
        var amenity = _mapper.Map<Amenity>(createAmenityDto);
        var createdAmenity = await _amenityRepository.CreateAsync(amenity);
        return _mapper.Map<AmenityDto>(createdAmenity);
    }

    public async Task<AmenityDto?> UpdateAmenityAsync(int id, UpdateAmenityDto updateAmenityDto)
    {
        var amenity = await _amenityRepository.GetByIdAsync(id);
        if (amenity == null)
            return null;

        if (!string.IsNullOrEmpty(updateAmenityDto.Name))
            amenity.Name = updateAmenityDto.Name;
        if (!string.IsNullOrEmpty(updateAmenityDto.Description))
            amenity.Description = updateAmenityDto.Description;
        if (!string.IsNullOrEmpty(updateAmenityDto.Icon))
            amenity.Icon = updateAmenityDto.Icon;

        var updatedAmenity = await _amenityRepository.UpdateAsync(amenity);
        return _mapper.Map<AmenityDto>(updatedAmenity);
    }

    public async Task<bool> DeleteAmenityAsync(int id)
    {
        return await _amenityRepository.DeleteAsync(id);
    }

    public async Task<bool> AddAmenityToRoomAsync(int roomId, int amenityId)
    {
        if (!await _roomRepository.ExistsAsync(roomId))
            throw new ArgumentException("Room not found", nameof(roomId));

        if (!await _amenityRepository.ExistsAsync(amenityId))
            throw new ArgumentException("Amenity not found", nameof(amenityId));

        var exists = await _context.RoomAmenities
            .AnyAsync(ra => ra.RoomId == roomId && ra.AmenityId == amenityId);

        if (exists)
            return false;

        var roomAmenity = new RoomAmenity
        {
            RoomId = roomId,
            AmenityId = amenityId
        };

        _context.RoomAmenities.Add(roomAmenity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAmenityFromRoomAsync(int roomId, int amenityId)
    {
        var roomAmenity = await _context.RoomAmenities
            .FirstOrDefaultAsync(ra => ra.RoomId == roomId && ra.AmenityId == amenityId);

        if (roomAmenity == null)
            return false;

        _context.RoomAmenities.Remove(roomAmenity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AmenityDto>> GetRoomAmenitiesAsync(int roomId)
    {
        var amenities = await _context.RoomAmenities
            .Where(ra => ra.RoomId == roomId)
            .Include(ra => ra.Amenity)
            .Select(ra => ra.Amenity)
            .ToListAsync();

        return _mapper.Map<IEnumerable<AmenityDto>>(amenities);
    }
}
