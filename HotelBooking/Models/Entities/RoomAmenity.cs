namespace HotelBooking.Models.Entities;

public class RoomAmenity
{
    public int RoomId { get; set; }
    public int AmenityId { get; set; }
    
    // Navigation properties
    public Room Room { get; set; } = null!;
    public Amenity Amenity { get; set; } = null!;
}
