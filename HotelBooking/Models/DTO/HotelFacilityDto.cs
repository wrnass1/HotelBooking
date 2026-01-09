namespace HotelBooking.Models.DTO;

public class HotelFacilityDto
{
    public int HotelId { get; set; }
    public int FacilityId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
}

public class AddHotelFacilityDto
{
    public int FacilityId { get; set; }
}
