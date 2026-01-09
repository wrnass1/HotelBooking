namespace HotelBooking.Models.DTO;

public class ApiKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class CreateApiKeyDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ExpiresAt { get; set; }
}
