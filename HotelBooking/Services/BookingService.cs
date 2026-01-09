using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IMapper _mapper;

    public BookingService(IBookingRepository bookingRepository, IRoomRepository roomRepository, IMapper mapper)
    {
        _bookingRepository = bookingRepository;
        _roomRepository = roomRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<BookingDto>> GetAllBookingsAsync()
    {
        var bookings = await _bookingRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<BookingDto>>(bookings);
    }

    public async Task<IEnumerable<BookingDto>> GetBookingsByRoomIdAsync(int roomId)
    {
        var bookings = await _bookingRepository.GetByRoomIdAsync(roomId);
        return _mapper.Map<IEnumerable<BookingDto>>(bookings);
    }

    public async Task<IEnumerable<BookingDto>> GetBookingsByGuestEmailAsync(string email)
    {
        var bookings = await _bookingRepository.GetByGuestEmailAsync(email);
        return _mapper.Map<IEnumerable<BookingDto>>(bookings);
    }

    public async Task<BookingDto?> GetBookingByIdAsync(int id)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);
        return booking == null ? null : _mapper.Map<BookingDto>(booking);
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingDto createBookingDto)
    {
        var room = await _roomRepository.GetByIdAsync(createBookingDto.RoomId);
        if (room == null)
            throw new ArgumentException("Room not found", nameof(createBookingDto.RoomId));

        if (!room.IsAvailable)
            throw new InvalidOperationException("Room is not available");

        if (createBookingDto.CheckInDate >= createBookingDto.CheckOutDate)
            throw new ArgumentException("Check-out date must be after check-in date");

        if (createBookingDto.CheckInDate < DateTime.Today)
            throw new ArgumentException("Check-in date cannot be in the past");

        var isAvailable = await _bookingRepository.IsRoomAvailableAsync(
            createBookingDto.RoomId,
            createBookingDto.CheckInDate,
            createBookingDto.CheckOutDate);

        if (!isAvailable)
            throw new InvalidOperationException("Room is not available for the selected dates");

        if (createBookingDto.NumberOfGuests > room.MaxOccupancy)
            throw new ArgumentException($"Number of guests exceeds room capacity (max: {room.MaxOccupancy})");

        var numberOfNights = (createBookingDto.CheckOutDate - createBookingDto.CheckInDate).Days;
        var totalPrice = room.PricePerNight * numberOfNights;

        var booking = _mapper.Map<Booking>(createBookingDto);
        booking.TotalPrice = totalPrice;
        booking.Status = "Confirmed";

        var createdBooking = await _bookingRepository.CreateAsync(booking);
        return _mapper.Map<BookingDto>(createdBooking);
    }

    public async Task<BookingDto?> UpdateBookingAsync(int id, UpdateBookingDto updateBookingDto)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);
        if (booking == null)
            return null;

        if (booking.Status == "Cancelled")
            throw new InvalidOperationException("Cannot update a cancelled booking");

        if (!string.IsNullOrEmpty(updateBookingDto.GuestName))
            booking.GuestName = updateBookingDto.GuestName;
        if (!string.IsNullOrEmpty(updateBookingDto.GuestEmail))
            booking.GuestEmail = updateBookingDto.GuestEmail;
        if (!string.IsNullOrEmpty(updateBookingDto.GuestPhone))
            booking.GuestPhone = updateBookingDto.GuestPhone;
        if (updateBookingDto.NumberOfGuests.HasValue)
            booking.NumberOfGuests = updateBookingDto.NumberOfGuests.Value;
        if (!string.IsNullOrEmpty(updateBookingDto.Status))
            booking.Status = updateBookingDto.Status;

        if (updateBookingDto.CheckInDate.HasValue || updateBookingDto.CheckOutDate.HasValue)
        {
            var checkIn = updateBookingDto.CheckInDate ?? booking.CheckInDate;
            var checkOut = updateBookingDto.CheckOutDate ?? booking.CheckOutDate;

            if (checkIn >= checkOut)
                throw new ArgumentException("Check-out date must be after check-in date");

            var room = await _roomRepository.GetByIdAsync(booking.RoomId);
            if (room == null)
                throw new InvalidOperationException("Room not found");

            var isAvailable = await _bookingRepository.IsRoomAvailableAsync(
                booking.RoomId,
                checkIn,
                checkOut,
                booking.Id);

            if (!isAvailable)
                throw new InvalidOperationException("Room is not available for the selected dates");

            booking.CheckInDate = checkIn;
            booking.CheckOutDate = checkOut;

            var numberOfNights = (checkOut - checkIn).Days;
            booking.TotalPrice = room.PricePerNight * numberOfNights;
        }

        var updatedBooking = await _bookingRepository.UpdateAsync(booking);
        return _mapper.Map<BookingDto>(updatedBooking);
    }

    public async Task<bool> DeleteBookingAsync(int id)
    {
        return await _bookingRepository.DeleteAsync(id);
    }

    public async Task<bool> CancelBookingAsync(int id)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);
        if (booking == null)
            return false;

        if (booking.Status == "Cancelled")
            return true;

        booking.Status = "Cancelled";
        await _bookingRepository.UpdateAsync(booking);
        return true;
    }
}
