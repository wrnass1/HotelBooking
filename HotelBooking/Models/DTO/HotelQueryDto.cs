namespace HotelBooking.Models.DTO;

public class HotelQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public int? MinStarRating { get; set; }
}
