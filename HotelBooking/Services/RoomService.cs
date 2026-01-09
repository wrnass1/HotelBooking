using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IHotelRepository _hotelRepository;
    private readonly IMapper _mapper;

    public RoomService(IRoomRepository roomRepository, IHotelRepository hotelRepository, IMapper mapper)
    {
        _roomRepository = roomRepository;
        _hotelRepository = hotelRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<RoomDto>> GetAllRoomsAsync()
    {
        var rooms = await _roomRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<IEnumerable<RoomDto>> GetRoomsByHotelIdAsync(int hotelId)
    {
        var rooms = await _roomRepository.GetByHotelIdAsync(hotelId);
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<IEnumerable<RoomDto>> GetAvailableRoomsAsync(int hotelId, DateTime checkIn, DateTime checkOut)
    {
        var rooms = await _roomRepository.GetAvailableRoomsAsync(hotelId, checkIn, checkOut);
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<RoomDto?> GetRoomByIdAsync(int id)
    {
        var room = await _roomRepository.GetByIdAsync(id);
        return room == null ? null : _mapper.Map<RoomDto>(room);
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomDto createRoomDto)
    {
        if (!await _hotelRepository.ExistsAsync(createRoomDto.HotelId))
            throw new ArgumentException("Hotel not found", nameof(createRoomDto.HotelId));

        var room = _mapper.Map<Room>(createRoomDto);
        var createdRoom = await _roomRepository.CreateAsync(room);
        return _mapper.Map<RoomDto>(createdRoom);
    }

    public async Task<RoomDto?> UpdateRoomAsync(int id, UpdateRoomDto updateRoomDto)
    {
        var room = await _roomRepository.GetByIdAsync(id);
        if (room == null)
            return null;

        if (!string.IsNullOrEmpty(updateRoomDto.RoomNumber))
            room.RoomNumber = updateRoomDto.RoomNumber;
        if (!string.IsNullOrEmpty(updateRoomDto.RoomType))
            room.RoomType = updateRoomDto.RoomType;
        if (updateRoomDto.PricePerNight.HasValue)
            room.PricePerNight = updateRoomDto.PricePerNight.Value;
        if (updateRoomDto.MaxOccupancy.HasValue)
            room.MaxOccupancy = updateRoomDto.MaxOccupancy.Value;
        if (!string.IsNullOrEmpty(updateRoomDto.Description))
            room.Description = updateRoomDto.Description;
        if (updateRoomDto.IsAvailable.HasValue)
            room.IsAvailable = updateRoomDto.IsAvailable.Value;

        var updatedRoom = await _roomRepository.UpdateAsync(room);
        return _mapper.Map<RoomDto>(updatedRoom);
    }

    public async Task<bool> DeleteRoomAsync(int id)
    {
        return await _roomRepository.DeleteAsync(id);
    }
}
