using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomService roomService, ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAllRooms()
    {
        try
        {
            var rooms = await _roomService.GetAllRoomsAsync();
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all rooms");
            return StatusCode(500, "An error occurred while retrieving rooms");
        }
    }

    [HttpGet("hotel/{hotelId}")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetRoomsByHotel(int hotelId)
    {
        try
        {
            var rooms = await _roomService.GetRoomsByHotelIdAsync(hotelId);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rooms for hotel {HotelId}", hotelId);
            return StatusCode(500, "An error occurred while retrieving rooms");
        }
    }

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAvailableRooms(
        [FromQuery] int hotelId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut)
    {
        try
        {
            var rooms = await _roomService.GetAvailableRoomsAsync(hotelId, checkIn, checkOut);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available rooms");
            return StatusCode(500, "An error occurred while retrieving available rooms");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoomDto>> GetRoom(int id)
    {
        try
        {
            var room = await _roomService.GetRoomByIdAsync(id);
            if (room == null)
                return NotFound($"Room with id {id} not found");

            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving the room");
        }
    }

    [HttpPost]
    public async Task<ActionResult<RoomDto>> CreateRoom([FromBody] CreateRoomDto createRoomDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.CreateRoomAsync(createRoomDto);
            return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, "An error occurred while creating the room");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RoomDto>> UpdateRoom(int id, [FromBody] UpdateRoomDto updateRoomDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var room = await _roomService.UpdateRoomAsync(id, updateRoomDto);
            if (room == null)
                return NotFound($"Room with id {id} not found");

            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room {RoomId}", id);
            return StatusCode(500, "An error occurred while updating the room");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        try
        {
            var deleted = await _roomService.DeleteRoomAsync(id);
            if (!deleted)
                return NotFound($"Room with id {id} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room {RoomId}", id);
            return StatusCode(500, "An error occurred while deleting the room");
        }
    }
}
