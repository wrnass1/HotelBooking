namespace HotelBooking.Models.Entities;

public class HotelFacility
{
    public int HotelId { get; set; }
    public int FacilityId { get; set; }
    
    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
