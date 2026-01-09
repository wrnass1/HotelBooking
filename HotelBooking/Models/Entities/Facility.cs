namespace HotelBooking.Models.Entities;

public class Facility
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Many-to-many relationship with Hotels
    public ICollection<HotelFacility> HotelFacilities { get; set; } = new List<HotelFacility>();
}
