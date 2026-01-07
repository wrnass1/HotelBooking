using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAllBookings()
    {
        try
        {
            var bookings = await _bookingService.GetAllBookingsAsync();
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all bookings");
            return StatusCode(500, "An error occurred while retrieving bookings");
        }
    }

    [HttpGet("room/{roomId}")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetBookingsByRoom(int roomId)
    {
        try
        {
            var bookings = await _bookingService.GetBookingsByRoomIdAsync(roomId);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookings for room {RoomId}", roomId);
            return StatusCode(500, "An error occurred while retrieving bookings");
        }
    }

    [HttpGet("guest/{email}")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetBookingsByGuest(string email)
    {
        try
        {
            var bookings = await _bookingService.GetBookingsByGuestEmailAsync(email);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bookings for guest {Email}", email);
            return StatusCode(500, "An error occurred while retrieving bookings");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        try
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null)
                return NotFound($"Booking with id {id} not found");

            return Ok(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking {BookingId}", id);
            return StatusCode(500, "An error occurred while retrieving the booking");
        }
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingDto createBookingDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var booking = await _bookingService.CreateBookingAsync(createBookingDto);
            return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, "An error occurred while creating the booking");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<BookingDto>> UpdateBooking(int id, [FromBody] UpdateBookingDto updateBookingDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var booking = await _bookingService.UpdateBookingAsync(id, updateBookingDto);
            if (booking == null)
                return NotFound($"Booking with id {id} not found");

            return Ok(booking);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId}", id);
            return StatusCode(500, "An error occurred while updating the booking");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        try
        {
            var deleted = await _bookingService.DeleteBookingAsync(id);
            if (!deleted)
                return NotFound($"Booking with id {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting booking {BookingId}", id);
            return StatusCode(500, "An error occurred while deleting the booking");
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        try
        {
            var cancelled = await _bookingService.CancelBookingAsync(id);
            if (!cancelled)
                return NotFound($"Booking with id {id} not found");

            return Ok(new { message = "Booking cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", id);
            return StatusCode(500, "An error occurred while cancelling the booking");
        }
    }
}
