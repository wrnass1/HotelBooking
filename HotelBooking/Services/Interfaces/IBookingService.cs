using HotelBooking.Models.DTO;

namespace HotelBooking.Services.Interfaces;

public interface IBookingService
{
    Task<IEnumerable<BookingDto>> GetAllBookingsAsync();
    Task<IEnumerable<BookingDto>> GetBookingsByRoomIdAsync(int roomId);
    Task<IEnumerable<BookingDto>> GetBookingsByGuestEmailAsync(string email);
    Task<BookingDto?> GetBookingByIdAsync(int id);
    Task<BookingDto> CreateBookingAsync(CreateBookingDto createBookingDto);
    Task<BookingDto?> UpdateBookingAsync(int id, UpdateBookingDto updateBookingDto);
    Task<bool> DeleteBookingAsync(int id);
    Task<bool> CancelBookingAsync(int id);
}
