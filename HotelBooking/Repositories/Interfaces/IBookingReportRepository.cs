using HotelBooking.Models.DTO;

namespace HotelBooking.Repositories.Interfaces;

public interface IBookingReportRepository
{
    Task<BookingStatisticsDto> GetBookingStatisticsAsync(int hotelId, DateTime startDate, DateTime endDate);
}

public class BookingStatisticsDto
{
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageBookingValue { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public Dictionary<string, int> BookingsByStatus { get; set; } = new();
    public Dictionary<string, decimal> RevenueByMonth { get; set; } = new();
}
