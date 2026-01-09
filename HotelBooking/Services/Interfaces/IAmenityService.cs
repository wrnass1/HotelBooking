using HotelBooking.Models.DTO;

namespace HotelBooking.Services.Interfaces;

public interface IAmenityService
{
    Task<IEnumerable<AmenityDto>> GetAllAmenitiesAsync();
    Task<AmenityDto?> GetAmenityByIdAsync(int id);
    Task<AmenityDto> CreateAmenityAsync(CreateAmenityDto createAmenityDto);
    Task<AmenityDto?> UpdateAmenityAsync(int id, UpdateAmenityDto updateAmenityDto);
    Task<bool> DeleteAmenityAsync(int id);
    Task<bool> AddAmenityToRoomAsync(int roomId, int amenityId);
    Task<bool> RemoveAmenityFromRoomAsync(int roomId, int amenityId);
    Task<IEnumerable<AmenityDto>> GetRoomAmenitiesAsync(int roomId);
}
