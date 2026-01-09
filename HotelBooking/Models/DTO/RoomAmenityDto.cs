namespace HotelBooking.Models.DTO;

public class RoomAmenityDto
{
    public int RoomId { get; set; }
    public int AmenityId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string AmenityName { get; set; } = string.Empty;
}

public class AddRoomAmenityDto
{
    public int AmenityId { get; set; }
}
