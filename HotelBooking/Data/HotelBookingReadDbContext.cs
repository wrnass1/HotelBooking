using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Data;

// Shares the entity mapping; DI provides a separate connection to the standby.
public sealed class HotelBookingReadDbContext : HotelBookingDbContext
{
    public HotelBookingReadDbContext(DbContextOptions<HotelBookingReadDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException("The replica context is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The replica context is read-only.");
}
