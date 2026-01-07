namespace HotelBooking.Models.DTO;

public class HotelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StarRating { get; set; }
}

public class CreateHotelDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StarRating { get; set; }
}

public class UpdateHotelDto
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Description { get; set; }
    public int? StarRating { get; set; }
}
