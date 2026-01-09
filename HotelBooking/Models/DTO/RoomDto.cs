namespace HotelBooking.Models.DTO;

public class RoomDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }
}

public class CreateRoomDto
{
    public int HotelId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public string? Description { get; set; }
}

public class UpdateRoomDto
{
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public decimal? PricePerNight { get; set; }
    public int? MaxOccupancy { get; set; }
    public string? Description { get; set; }
    public bool? IsAvailable { get; set; }
}
