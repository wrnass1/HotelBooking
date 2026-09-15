using Dapper;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Sharding;
using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Repositories;

public sealed class ShardedBookingRepository(BookingShardStore store, HotelBookingDbContext primary) : IBookingRepository
{
    private async Task<IEnumerable<Booking>> WithRoomsAsync(List<Booking> bookings)
    {
        var roomIds = bookings.Select(b => b.RoomId).Distinct().ToArray();
        var rooms = await primary.Rooms.AsNoTracking().Include(r => r.Hotel)
            .Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        foreach (var booking in bookings)
            booking.Room = rooms.TryGetValue(booking.RoomId, out var room) ? room
                : throw new InvalidOperationException($"Room {booking.RoomId} is missing on Primary.");
        return bookings;
    }

    public async Task<IEnumerable<Booking>> GetAllAsync() => await WithRoomsAsync(await store.ReadAsync());
    public async Task<IEnumerable<Booking>> GetByRoomIdAsync(int roomId) =>
        await WithRoomsAsync(await store.ReadAsync("\"RoomId\"=@RoomId", new { RoomId = roomId }));
    public async Task<IEnumerable<Booking>> GetByGuestEmailAsync(string email) =>
        await WithRoomsAsync(await store.ReadAsync("\"GuestEmail\"=@Email", new { Email = email }));
    public async Task<Booking?> GetByIdAsync(int id) => id <= 0 ? null :
        (await WithRoomsAsync(await store.ReadAsync("\"Id\"=@Id", new { Id = id }, id))).SingleOrDefault();
    public Task<bool> ExistsAsync(int id) => id <= 0 ? Task.FromResult(false) :
        store.AnyAsync("\"Id\"=@Id", new { Id = id }, id);
    public Task<bool> DeleteAsync(int id) => id <= 0 ? Task.FromResult(false) : store.DeleteAsync(id);

    public async Task<Booking> CreateAsync(Booking booking)
    {
        // The existing Primary sequence gives unique IDs across all shards (gaps are allowed).
        booking.Id = await primary.Database.GetDbConnection().ExecuteScalarAsync<int>(
            "SELECT nextval(pg_get_serial_sequence('public.\"Bookings\"', 'Id'))");
        booking.CreatedAt = DateTime.UtcNow;
        await store.InsertAsync(booking);
        return (await WithRoomsAsync([booking])).Single();
    }

    public async Task<Booking> UpdateAsync(Booking booking)
    {
        booking.UpdatedAt = DateTime.UtcNow;
        await store.UpdateAsync(booking);
        return booking;
    }

    public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null) =>
        !await store.AnyAsync("""
            "RoomId"=@RoomId AND "Status" <> 'Cancelled'
            AND "CheckInDate" < @CheckOut::date AND "CheckOutDate" > @CheckIn::date
            AND (@ExcludeId IS NULL OR "Id" <> @ExcludeId)
            """, new { RoomId = roomId, CheckIn = checkIn.Date, CheckOut = checkOut.Date, ExcludeId = excludeBookingId });
}
