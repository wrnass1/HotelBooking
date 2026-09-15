using System.Data;
using Dapper;
using HotelBooking.Data;
using HotelBooking.Repositories.Interfaces;
using Npgsql;
using HotelBooking.Sharding;

namespace HotelBooking.Repositories;

public class BookingReportRepository : IBookingReportRepository
{
    private readonly string _connectionString;
    private readonly ILogger<BookingReportRepository> _logger;
    private readonly BookingShardStore? _bookingShards;

    public BookingReportRepository(IConfiguration configuration, ILogger<BookingReportRepository> logger,
        BookingShardStore? bookingShards = null)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
        _logger = logger;
        _bookingShards = bookingShards;
    }

    public async Task<BookingStatisticsDto> GetBookingStatisticsAsync(int hotelId, DateTime startDate, DateTime endDate)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        if (_bookingShards != null)
        {
            var roomIds = (await connection.QueryAsync<int>(
                "SELECT \"Id\" FROM public.\"Rooms\" WHERE \"HotelId\"=@HotelId", new { HotelId = hotelId })).ToArray();
            var bookings = await _bookingShards.ReadAsync("""
                "RoomId"=ANY(@RoomIds) AND "CheckInDate" >= @StartDate::date AND "CheckInDate" <= @EndDate::date
                """, new { RoomIds = roomIds, StartDate = startDate.Date, EndDate = endDate.Date });
            return new BookingStatisticsDto
            {
                TotalBookings = bookings.Count,
                TotalRevenue = bookings.Sum(b => b.TotalPrice),
                AverageBookingValue = bookings.Count == 0 ? 0 : bookings.Average(b => b.TotalPrice),
                ConfirmedBookings = bookings.Count(b => b.Status == "Confirmed"),
                CancelledBookings = bookings.Count(b => b.Status == "Cancelled"),
                BookingsByStatus = bookings.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count()),
                RevenueByMonth = bookings.Where(b => b.Status != "Cancelled")
                    .GroupBy(b => b.CheckInDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
                    .ToDictionary(g => g.Key, g => g.Sum(b => b.TotalPrice))
            };
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var sql = @"
                SELECT 
                    COUNT(*) as TotalBookings,
                    COALESCE(SUM(TotalPrice), 0) as TotalRevenue,
                    COALESCE(AVG(TotalPrice), 0) as AverageBookingValue,
                    COUNT(CASE WHEN Status = 'Confirmed' THEN 1 END) as ConfirmedBookings,
                    COUNT(CASE WHEN Status = 'Cancelled' THEN 1 END) as CancelledBookings
                FROM ""Bookings"" b
                INNER JOIN ""Rooms"" r ON b.""RoomId"" = r.""Id""
                WHERE r.""HotelId"" = @HotelId
                AND b.""CheckInDate"" >= @StartDate
                AND b.""CheckInDate"" <= @EndDate";

            var stats = await connection.QueryFirstOrDefaultAsync<BookingStatisticsDto>(sql, new
            {
                HotelId = hotelId,
                StartDate = startDate,
                EndDate = endDate
            }, transaction);

            if (stats == null)
            {
                stats = new BookingStatisticsDto();
            }

            // Get bookings by status
            var statusSql = @"
                SELECT Status, COUNT(*) as Count
                FROM ""Bookings"" b
                INNER JOIN ""Rooms"" r ON b.""RoomId"" = r.""Id""
                WHERE r.""HotelId"" = @HotelId
                AND b.""CheckInDate"" >= @StartDate
                AND b.""CheckInDate"" <= @EndDate
                GROUP BY Status";

            var statusData = await connection.QueryAsync<(string Status, int Count)>(statusSql, new
            {
                HotelId = hotelId,
                StartDate = startDate,
                EndDate = endDate
            }, transaction);

            stats.BookingsByStatus = statusData.ToDictionary(x => x.Status, x => x.Count);

            // Get revenue by month
            var revenueSql = @"
                SELECT 
                    TO_CHAR(b.""CheckInDate"", 'YYYY-MM') as Month,
                    COALESCE(SUM(b.""TotalPrice""), 0) as Revenue
                FROM ""Bookings"" b
                INNER JOIN ""Rooms"" r ON b.""RoomId"" = r.""Id""
                WHERE r.""HotelId"" = @HotelId
                AND b.""CheckInDate"" >= @StartDate
                AND b.""CheckInDate"" <= @EndDate
                AND b.""Status"" != 'Cancelled'
                GROUP BY TO_CHAR(b.""CheckInDate"", 'YYYY-MM')
                ORDER BY Month";

            var revenueData = await connection.QueryAsync<(string Month, decimal Revenue)>(revenueSql, new
            {
                HotelId = hotelId,
                StartDate = startDate,
                EndDate = endDate
            }, transaction);

            stats.RevenueByMonth = revenueData.ToDictionary(x => x.Month, x => x.Revenue);

            transaction.Commit();
            return stats;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error getting booking statistics for hotel {HotelId}", hotelId);
            throw;
        }
    }
}
