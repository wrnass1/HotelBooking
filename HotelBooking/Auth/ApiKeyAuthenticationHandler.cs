using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;

namespace HotelBooking.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly HotelBookingDbContext _context;
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        HotelBookingDbContext context)
        : base(options, logger, encoder)
    {
        _context = context;
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKeyEntity = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Key == apiKey);

        if (apiKeyEntity == null)
        {
            _logger.LogWarning("Invalid API key attempted: {ApiKey}", apiKey);
            return AuthenticateResult.Fail("Invalid API key");
        }

        if (!apiKeyEntity.IsActive)
        {
            _logger.LogWarning("Inactive API key attempted: {ApiKeyId}", apiKeyEntity.Id);
            return AuthenticateResult.Fail("API key is inactive");
        }

        if (apiKeyEntity.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key attempted: {ApiKeyId}", apiKeyEntity.Id);
            return AuthenticateResult.Fail("API key has expired");
        }

        // Update last used timestamp
        apiKeyEntity.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKeyEntity.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKeyEntity.Name),
            new Claim("ApiKey", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
