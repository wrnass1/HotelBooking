using HotelBooking.Models.DTO;

namespace HotelBooking.Services.Interfaces;

public interface IRoomService
{
    Task<IEnumerable<RoomDto>> GetAllRoomsAsync();
    Task<IEnumerable<RoomDto>> GetRoomsByHotelIdAsync(int hotelId);
    Task<IEnumerable<RoomDto>> GetAvailableRoomsAsync(int hotelId, DateTime checkIn, DateTime checkOut);
    Task<RoomDto?> GetRoomByIdAsync(int id);
    Task<RoomDto> CreateRoomAsync(CreateRoomDto createRoomDto);
    Task<RoomDto?> UpdateRoomAsync(int id, UpdateRoomDto updateRoomDto);
    Task<bool> DeleteRoomAsync(int id);
}
