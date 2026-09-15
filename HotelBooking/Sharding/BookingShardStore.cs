using Dapper;
using HotelBooking.Models.Entities;
using Npgsql;
using NpgsqlTypes;

namespace HotelBooking.Sharding;

public sealed class BookingShardStore
{
    // Npgsql 10 exposes DATE as DateOnly; the existing Booking entity uses DateTime.
    internal const string BookingColumns = """
        "Id", "RoomId", "GuestName", "GuestEmail", "GuestPhone",
        "CheckInDate"::timestamp AS "CheckInDate", "CheckOutDate"::timestamp AS "CheckOutDate",
        "NumberOfGuests", "TotalPrice", "Status", "CreatedAt", "UpdatedAt"
        """;
    private readonly Dictionary<string, string> _connections;
    public ShardRouter Router { get; }
    public IEnumerable<string> Shards => _connections.Keys.Order(StringComparer.Ordinal);

    public BookingShardStore(IConfiguration configuration)
    {
        _connections = configuration.GetSection("Sharding:Connections").GetChildren()
            .ToDictionary(x => "shard-" + x.Key, x => x.Value ?? throw new InvalidOperationException("Missing shard connection."));
        Router = new ShardRouter(_connections.Keys,
            configuration.GetValue("Sharding:Strategy", ShardStrategy.Modulo),
            configuration.GetValue("Sharding:VirtualNodes", 1));
    }

    public async Task<NpgsqlConnection> OpenAsync(string shard)
    {
        var connection = new NpgsqlConnection(_connections[shard]);
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task ValidateLayoutAsync()
    {
        foreach (var shard in Shards)
        {
            await using var connection = await OpenAsync(shard);
            var layout = await connection.QuerySingleAsync<string>("SELECT fingerprint FROM public.sharding_layout WHERE singleton");
            if (layout != Router.Fingerprint)
                throw new InvalidOperationException($"Layout mismatch on {shard}. Migrate data before changing the routing configuration.");
        }
    }

    public async Task<List<Booking>> ReadAsync(string where = "true", object? parameters = null, int? id = null)
    {
        var targets = id.HasValue ? new[] { Router.Route(id.Value) } : Shards.ToArray();
        var batches = await Task.WhenAll(targets.Select(async shard =>
        {
            await using var connection = await OpenAsync(shard);
            return await connection.QueryAsync<Booking>($"SELECT {BookingColumns} FROM public.\"Bookings\" WHERE {where}", parameters);
        }));
        return batches.SelectMany(x => x).OrderBy(x => x.Id).ToList();
    }

    public async Task<bool> AnyAsync(string where, object parameters, int? id = null)
    {
        var targets = id.HasValue ? new[] { Router.Route(id.Value) } : Shards.ToArray();
        var results = await Task.WhenAll(targets.Select(async shard =>
        {
            await using var connection = await OpenAsync(shard);
            return await connection.ExecuteScalarAsync<bool>($"SELECT EXISTS (SELECT FROM public.\"Bookings\" WHERE {where})", parameters);
        }));
        return results.Any(x => x);
    }

    public async Task<int[]> BusyRoomsAsync(DateTime checkIn, DateTime checkOut)
    {
        var batches = await Task.WhenAll(Shards.Select(async shard =>
        {
            await using var connection = await OpenAsync(shard);
            return await connection.QueryAsync<int>("""
                SELECT DISTINCT "RoomId" FROM public."Bookings"
                WHERE "Status" <> 'Cancelled' AND "CheckInDate" < @CheckOut::date AND "CheckOutDate" > @CheckIn::date
                """, new { CheckIn = checkIn.Date, CheckOut = checkOut.Date });
        }));
        return batches.SelectMany(x => x).Distinct().ToArray();
    }

    public async Task InsertAsync(Booking booking)
    {
        await using var connection = await OpenAsync(Router.Route(booking.Id));
        await connection.ExecuteAsync("""
            INSERT INTO public."Bookings" ("Id", "RoomId", "GuestName", "GuestEmail", "GuestPhone",
                "CheckInDate", "CheckOutDate", "NumberOfGuests", "TotalPrice", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@Id, @RoomId, @GuestName, @GuestEmail, @GuestPhone, @CheckInDate::date,
                @CheckOutDate::date, @NumberOfGuests, @TotalPrice, @Status, @CreatedAt, @UpdatedAt)
            """, Parameters(booking));
    }

    public async Task UpdateAsync(Booking booking)
    {
        await using var connection = await OpenAsync(Router.Route(booking.Id));
        var changed = await connection.ExecuteAsync("""
            UPDATE public."Bookings" SET "GuestName"=@GuestName, "GuestEmail"=@GuestEmail,
                "GuestPhone"=@GuestPhone, "CheckInDate"=@CheckInDate::date, "CheckOutDate"=@CheckOutDate::date,
                "NumberOfGuests"=@NumberOfGuests, "TotalPrice"=@TotalPrice, "Status"=@Status, "UpdatedAt"=@UpdatedAt
            WHERE "Id"=@Id AND "RoomId"=@RoomId
            """, Parameters(booking));
        if (changed != 1) throw new InvalidOperationException("Booking was removed or its room was changed.");
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = await OpenAsync(Router.Route(id));
        return await connection.ExecuteAsync("DELETE FROM public.\"Bookings\" WHERE \"Id\"=@Id", new { Id = id }) == 1;
    }

    private static object Parameters(Booking b) => new
    {
        b.Id, b.RoomId, b.GuestName, b.GuestEmail, b.GuestPhone,
        CheckInDate = DateTime.SpecifyKind(b.CheckInDate.Date, DateTimeKind.Unspecified),
        CheckOutDate = DateTime.SpecifyKind(b.CheckOutDate.Date, DateTimeKind.Unspecified),
        b.NumberOfGuests, b.TotalPrice, b.Status,
        CreatedAt = DateTime.SpecifyKind(b.CreatedAt, DateTimeKind.Unspecified),
        UpdatedAt = b.UpdatedAt.HasValue ? DateTime.SpecifyKind(b.UpdatedAt.Value, DateTimeKind.Unspecified) : (DateTime?)null
    };

    // Bulk loading uses exactly the same Router and Booking model as normal API writes.
    public async Task CopyAsync(IEnumerable<Booking> bookings)
    {
        foreach (var batch in bookings.GroupBy(b => Router.Route(b.Id)))
        {
            await using var connection = await OpenAsync(batch.Key);
            await using var writer = await connection.BeginBinaryImportAsync("""
                COPY public."Bookings" ("Id", "RoomId", "GuestName", "GuestEmail", "GuestPhone", "CheckInDate",
                    "CheckOutDate", "NumberOfGuests", "TotalPrice", "Status", "CreatedAt", "UpdatedAt") FROM STDIN (FORMAT BINARY)
                """);
            foreach (var b in batch)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(b.Id, NpgsqlDbType.Integer);
                await writer.WriteAsync(b.RoomId, NpgsqlDbType.Integer);
                await writer.WriteAsync(b.GuestName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(b.GuestEmail, NpgsqlDbType.Varchar);
                await writer.WriteAsync(b.GuestPhone, NpgsqlDbType.Varchar);
                await writer.WriteAsync(DateOnly.FromDateTime(b.CheckInDate), NpgsqlDbType.Date);
                await writer.WriteAsync(DateOnly.FromDateTime(b.CheckOutDate), NpgsqlDbType.Date);
                await writer.WriteAsync(b.NumberOfGuests, NpgsqlDbType.Integer);
                await writer.WriteAsync(b.TotalPrice, NpgsqlDbType.Numeric);
                await writer.WriteAsync(b.Status, NpgsqlDbType.Varchar);
                await writer.WriteAsync(DateTime.SpecifyKind(b.CreatedAt, DateTimeKind.Unspecified), NpgsqlDbType.Timestamp);
                if (b.UpdatedAt is { } updated)
                    await writer.WriteAsync(DateTime.SpecifyKind(updated, DateTimeKind.Unspecified), NpgsqlDbType.Timestamp);
                else await writer.WriteNullAsync();
            }
            await writer.CompleteAsync();
        }
    }
}
