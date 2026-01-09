using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AmenitiesController : ControllerBase
{
    private readonly IAmenityService _amenityService;
    private readonly ILogger<AmenitiesController> _logger;

    public AmenitiesController(IAmenityService amenityService, ILogger<AmenitiesController> logger)
    {
        _amenityService = amenityService;
        _logger = logger;
    }

    /// <summary>
    /// Get all amenities
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AmenityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<AmenityDto>>> GetAllAmenities()
    {
        var amenities = await _amenityService.GetAllAmenitiesAsync();
        return Ok(amenities);
    }

    /// <summary>
    /// Get amenity by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AmenityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<AmenityDto>> GetAmenity(int id)
    {
        var amenity = await _amenityService.GetAmenityByIdAsync(id);
        if (amenity == null)
            return NotFound();

        return Ok(amenity);
    }

    /// <summary>
    /// Create a new amenity
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AmenityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<AmenityDto>> CreateAmenity([FromBody] CreateAmenityDto createAmenityDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var amenity = await _amenityService.CreateAmenityAsync(createAmenityDto);
        return CreatedAtAction(nameof(GetAmenity), new { id = amenity.Id }, amenity);
    }

    /// <summary>
    /// Update amenity
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AmenityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<AmenityDto>> UpdateAmenity(int id, [FromBody] UpdateAmenityDto updateAmenityDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var amenity = await _amenityService.UpdateAmenityAsync(id, updateAmenityDto);
        if (amenity == null)
            return NotFound();

        return Ok(amenity);
    }

    /// <summary>
    /// Delete amenity
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAmenity(int id)
    {
        var deleted = await _amenityService.DeleteAmenityAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get amenities for a room
    /// </summary>
    [HttpGet("room/{roomId}")]
    [ProducesResponseType(typeof(IEnumerable<AmenityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<AmenityDto>>> GetRoomAmenities(int roomId)
    {
        var amenities = await _amenityService.GetRoomAmenitiesAsync(roomId);
        return Ok(amenities);
    }

    /// <summary>
    /// Add amenity to room
    /// </summary>
    [HttpPost("room/{roomId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddAmenityToRoom(int roomId, [FromBody] AddRoomAmenityDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _amenityService.AddAmenityToRoomAsync(roomId, dto.AmenityId);
        if (!result)
            return BadRequest("Amenity already added to room or invalid IDs");

        return Ok(new { message = "Amenity added to room successfully" });
    }

    /// <summary>
    /// Remove amenity from room
    /// </summary>
    [HttpDelete("room/{roomId}/amenity/{amenityId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RemoveAmenityFromRoom(int roomId, int amenityId)
    {
        var result = await _amenityService.RemoveAmenityFromRoomAsync(roomId, amenityId);
        if (!result)
            return NotFound();

        return NoContent();
    }
}
