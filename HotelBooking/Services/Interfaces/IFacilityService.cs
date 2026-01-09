using HotelBooking.Models.DTO;

namespace HotelBooking.Services.Interfaces;

public interface IFacilityService
{
    Task<IEnumerable<FacilityDto>> GetAllFacilitiesAsync();
    Task<FacilityDto?> GetFacilityByIdAsync(int id);
    Task<FacilityDto> CreateFacilityAsync(CreateFacilityDto createFacilityDto);
    Task<FacilityDto?> UpdateFacilityAsync(int id, UpdateFacilityDto updateFacilityDto);
    Task<bool> DeleteFacilityAsync(int id);
    Task<bool> AddFacilityToHotelAsync(int hotelId, int facilityId);
    Task<bool> RemoveFacilityFromHotelAsync(int hotelId, int facilityId);
    Task<IEnumerable<FacilityDto>> GetHotelFacilitiesAsync(int hotelId);
}
