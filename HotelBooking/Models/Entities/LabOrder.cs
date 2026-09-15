namespace HotelBooking.Models.Entities;

/// <summary>
/// Isolated data set for the index and EXPLAIN ANALYZE laboratory work.
/// It is not used by the hotel-booking application.
/// </summary>
public class LabOrder
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public long ProductId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
