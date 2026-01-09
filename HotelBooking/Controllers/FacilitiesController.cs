using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class FacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilityService;
    private readonly ILogger<FacilitiesController> _logger;

    public FacilitiesController(IFacilityService facilityService, ILogger<FacilitiesController> logger)
    {
        _facilityService = facilityService;
        _logger = logger;
    }

    /// <summary>
    /// Get all facilities
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FacilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<FacilityDto>>> GetAllFacilities()
    {
        var facilities = await _facilityService.GetAllFacilitiesAsync();
        return Ok(facilities);
    }

    /// <summary>
    /// Get facility by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FacilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<FacilityDto>> GetFacility(int id)
    {
        var facility = await _facilityService.GetFacilityByIdAsync(id);
        if (facility == null)
            return NotFound();

        return Ok(facility);
    }

    /// <summary>
    /// Create a new facility
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FacilityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<FacilityDto>> CreateFacility([FromBody] CreateFacilityDto createFacilityDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var facility = await _facilityService.CreateFacilityAsync(createFacilityDto);
        return CreatedAtAction(nameof(GetFacility), new { id = facility.Id }, facility);
    }

    /// <summary>
    /// Update facility
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(FacilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<FacilityDto>> UpdateFacility(int id, [FromBody] UpdateFacilityDto updateFacilityDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var facility = await _facilityService.UpdateFacilityAsync(id, updateFacilityDto);
        if (facility == null)
            return NotFound();

        return Ok(facility);
    }

    /// <summary>
    /// Delete facility
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFacility(int id)
    {
        var deleted = await _facilityService.DeleteFacilityAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get facilities for a hotel
    /// </summary>
    [HttpGet("hotel/{hotelId}")]
    [ProducesResponseType(typeof(IEnumerable<FacilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<FacilityDto>>> GetHotelFacilities(int hotelId)
    {
        var facilities = await _facilityService.GetHotelFacilitiesAsync(hotelId);
        return Ok(facilities);
    }

    /// <summary>
    /// Add facility to hotel
    /// </summary>
    [HttpPost("hotel/{hotelId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AddFacilityToHotel(int hotelId, [FromBody] AddHotelFacilityDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _facilityService.AddFacilityToHotelAsync(hotelId, dto.FacilityId);
        if (!result)
            return BadRequest("Facility already added to hotel or invalid IDs");

        return Ok(new { message = "Facility added to hotel successfully" });
    }

    /// <summary>
    /// Remove facility from hotel
    /// </summary>
    [HttpDelete("hotel/{hotelId}/facility/{facilityId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RemoveFacilityFromHotel(int hotelId, int facilityId)
    {
        var result = await _facilityService.RemoveFacilityFromHotelAsync(hotelId, facilityId);
        if (!result)
            return NotFound();

        return NoContent();
    }
}
