using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HotelsController : ControllerBase
{
    private readonly IHotelService _hotelService;
    private readonly ILogger<HotelsController> _logger;

    public HotelsController(IHotelService hotelService, ILogger<HotelsController> logger)
    {
        _hotelService = hotelService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HotelDto>>> GetAllHotels()
    {
        try
        {
            var hotels = await _hotelService.GetAllHotelsAsync();
            return Ok(hotels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all hotels");
            return StatusCode(500, "An error occurred while retrieving hotels");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HotelDto>> GetHotel(int id)
    {
        try
        {
            var hotel = await _hotelService.GetHotelByIdAsync(id);
            if (hotel == null)
                return NotFound($"Hotel with id {id} not found");

            return Ok(hotel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hotel {HotelId}", id);
            return StatusCode(500, "An error occurred while retrieving the hotel");
        }
    }

    [HttpPost]
    public async Task<ActionResult<HotelDto>> CreateHotel([FromBody] CreateHotelDto createHotelDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var hotel = await _hotelService.CreateHotelAsync(createHotelDto);
            return CreatedAtAction(nameof(GetHotel), new { id = hotel.Id }, hotel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hotel");
            return StatusCode(500, "An error occurred while creating the hotel");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<HotelDto>> UpdateHotel(int id, [FromBody] UpdateHotelDto updateHotelDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var hotel = await _hotelService.UpdateHotelAsync(id, updateHotelDto);
            if (hotel == null)
                return NotFound($"Hotel with id {id} not found");

            return Ok(hotel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hotel {HotelId}", id);
            return StatusCode(500, "An error occurred while updating the hotel");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHotel(int id)
    {
        try
        {
            var deleted = await _hotelService.DeleteHotelAsync(id);
            if (!deleted)
                return NotFound($"Hotel with id {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hotel {HotelId}", id);
            return StatusCode(500, "An error occurred while deleting the hotel");
        }
    }
}
