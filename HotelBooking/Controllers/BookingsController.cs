using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all bookings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAllBookings()
    {
        var bookings = await _bookingService.GetAllBookingsAsync();
        return Ok(bookings);
    }

    /// <summary>
    /// Get bookings by room ID
    /// </summary>
    [HttpGet("room/{roomId}")]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetBookingsByRoom(int roomId)
    {
        var bookings = await _bookingService.GetBookingsByRoomIdAsync(roomId);
        return Ok(bookings);
    }

    /// <summary>
    /// Get bookings by guest email
    /// </summary>
    [HttpGet("guest/{email}")]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetBookingsByGuest(string email)
    {
        var bookings = await _bookingService.GetBookingsByGuestEmailAsync(email);
        return Ok(bookings);
    }

    /// <summary>
    /// Get booking by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking == null)
            return NotFound();

        return Ok(booking);
    }

    /// <summary>
    /// Create a new booking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingDto createBookingDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var booking = await _bookingService.CreateBookingAsync(createBookingDto);
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }

    /// <summary>
    /// Update booking
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<BookingDto>> UpdateBooking(int id, [FromBody] UpdateBookingDto updateBookingDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var booking = await _bookingService.UpdateBookingAsync(id, updateBookingDto);
        if (booking == null)
            return NotFound();

        return Ok(booking);
    }

    /// <summary>
    /// Delete booking
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        var deleted = await _bookingService.DeleteBookingAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Cancel booking
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var cancelled = await _bookingService.CancelBookingAsync(id);
        if (!cancelled)
            return NotFound();

        return Ok(new { message = "Booking cancelled successfully" });
    }
}
