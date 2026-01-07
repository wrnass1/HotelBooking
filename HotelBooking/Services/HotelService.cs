using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Services;

public class HotelService : IHotelService
{
    private readonly IHotelRepository _hotelRepository;
    private readonly IMapper _mapper;

    public HotelService(IHotelRepository hotelRepository, IMapper mapper)
    {
        _hotelRepository = hotelRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsAsync()
    {
        var hotels = await _hotelRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }

    public async Task<HotelDto?> GetHotelByIdAsync(int id)
    {
        var hotel = await _hotelRepository.GetByIdAsync(id);
        return hotel == null ? null : _mapper.Map<HotelDto>(hotel);
    }

    public async Task<HotelDto> CreateHotelAsync(CreateHotelDto createHotelDto)
    {
        var hotel = _mapper.Map<Hotel>(createHotelDto);
        var createdHotel = await _hotelRepository.CreateAsync(hotel);
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
        return _mapper.Map<HotelDto>(updatedHotel);
    }

    public async Task<bool> DeleteHotelAsync(int id)
    {
        return await _hotelRepository.DeleteAsync(id);
    }
}
