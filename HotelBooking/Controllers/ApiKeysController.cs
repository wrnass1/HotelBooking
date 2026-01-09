using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models.DTO;
using HotelBooking.Services.Interfaces;

namespace HotelBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(IApiKeyService apiKeyService, ILogger<ApiKeysController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Get all API keys
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetAllApiKeys()
    {
        var apiKeys = await _apiKeyService.GetAllApiKeysAsync();
        return Ok(apiKeys);
    }

    /// <summary>
    /// Get API key by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiKeyDto>> GetApiKey(int id)
    {
        var apiKey = await _apiKeyService.GetApiKeyByIdAsync(id);
        if (apiKey == null)
            return NotFound();

        return Ok(apiKey);
    }

    /// <summary>
    /// Create a new API key
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiKeyDto>> CreateApiKey([FromBody] CreateApiKeyDto createApiKeyDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var apiKey = await _apiKeyService.CreateApiKeyAsync(createApiKeyDto);
        return CreatedAtAction(nameof(GetApiKey), new { id = apiKey.Id }, apiKey);
    }

    /// <summary>
    /// Update API key
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiKeyDto>> UpdateApiKey(int id, [FromBody] ApiKeyDto updateApiKeyDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var apiKey = await _apiKeyService.UpdateApiKeyAsync(id, updateApiKeyDto);
        if (apiKey == null)
            return NotFound();

        return Ok(apiKey);
    }

    /// <summary>
    /// Delete API key
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteApiKey(int id)
    {
        var deleted = await _apiKeyService.DeleteApiKeyAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
