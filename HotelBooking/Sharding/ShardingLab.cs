using System.Globalization;
using System.Text.Json;
using Dapper;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Sharding;

// Runs inside the existing backend assembly, using the same storage and Router as HTTP.
public static class ShardingLab
{
    private const string DatasetPredicate = "\"GuestEmail\" LIKE 'lab05.booking.%@example.test'";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        var index = Array.IndexOf(args, "--lab05");
        var command = index + 1 < args.Length ? args[index + 1] : "compare";
        var store = services.GetRequiredService<BookingShardStore>();
        var primary = services.GetRequiredService<HotelBookingDbContext>();
        object result = command switch
        {
            "import" => await ImportAsync(primary, store),
            "seed" => await SeedAsync(primary, store),
            "compare" => await CompareAsync(store),
            "verify" => await VerifyAsync(services, store),
            _ => throw new ArgumentException("Use --lab05 import|seed|compare|verify")
        };
        Console.WriteLine("LAB05_RESULT:" + JsonSerializer.Serialize(new { utc = DateTime.UtcNow, command, result }, Json));
    }

    private static async Task<object> ImportAsync(HotelBookingDbContext primary, BookingShardStore store)
    {
        // Run with API writers stopped. Preserve the Primary table as a pre-sharding snapshot.
        var source = (await primary.Database.GetDbConnection().QueryAsync<Booking>(
            $"SELECT {BookingShardStore.BookingColumns} FROM public.\"Bookings\"")).ToList();
        var existing = (await store.ReadAsync()).ToDictionary(b => b.Id);
        foreach (var b in source.Where(b => existing.ContainsKey(b.Id)))
            if (JsonSerializer.Serialize(b, Json) != JsonSerializer.Serialize(existing[b.Id], Json))
                throw new InvalidOperationException($"Conflicting booking {b.Id}; source was not overwritten.");
        var missing = source.Where(b => !existing.ContainsKey(b.Id)).ToList();
        await store.CopyAsync(missing);
        var final = (await store.ReadAsync()).ToDictionary(b => b.Id);
        if (source.Any(b => !final.ContainsKey(b.Id))) throw new InvalidOperationException("Incomplete import.");
        return new { sourceRows = source.Count, imported = missing.Count, sourcePreserved = true };
    }

    private static async Task<object> SeedAsync(HotelBookingDbContext primary, BookingShardStore store)
    {
        var existing = await store.ReadAsync(DatasetPredicate);
        if (existing.Count == 100_000) return new { rows = existing.Count, reused = true };
        if (existing.Count != 0) throw new InvalidOperationException("Partial lab05 dataset; inspect it before retrying.");
        var connection = primary.Database.GetDbConnection();
        var hotelId = await connection.QuerySingleOrDefaultAsync<int?>("""
            SELECT "Id" FROM public."Hotels" WHERE "Name"='Lab05 Sharding Hotel' AND "City"='Lab05'
            """) ?? await connection.QuerySingleAsync<int>("""
            INSERT INTO public."Hotels" ("Name", "Address", "City", "Country", "StarRating")
            VALUES ('Lab05 Sharding Hotel', 'Laboratory address', 'Lab05', 'Lab05', 3) RETURNING "Id"
            """);
        var rooms = new List<int>();
        for (var i = 0; i < 100; i++)
        {
            var number = "L05-" + i.ToString("D3", CultureInfo.InvariantCulture);
            rooms.Add(await connection.QuerySingleAsync<int>("""
                INSERT INTO public."Rooms" ("HotelId", "RoomNumber", "RoomType", "PricePerNight", "MaxOccupancy")
                VALUES (@HotelId, @Number, 'Double', 200, 2)
                ON CONFLICT ("HotelId", "RoomNumber") DO UPDATE SET "RoomNumber"=EXCLUDED."RoomNumber"
                RETURNING "Id"
                """, new { HotelId = hotelId, Number = number }));
        }
        var ids = (await connection.QueryAsync<int>("""
            SELECT nextval(pg_get_serial_sequence('public."Bookings"', 'Id'))::int FROM generate_series(1,100000)
            """)).ToArray();
        var bookings = Enumerable.Range(0, ids.Length).Select(i => new Booking
        {
            Id = ids[i], RoomId = rooms[i % rooms.Count], GuestName = $"Lab05 Guest {i + 1}",
            GuestEmail = $"lab05.booking.{i + 1:D6}@example.test", NumberOfGuests = 2, TotalPrice = 200,
            CheckInDate = new DateTime(2030, 1, 1).AddDays(i / rooms.Count * 2),
            CheckOutDate = new DateTime(2030, 1, 2).AddDays(i / rooms.Count * 2),
            Status = i % 10 == 0 ? "Cancelled" : "Confirmed", CreatedAt = DateTime.UtcNow
        }).ToArray();
        await store.CopyAsync(bookings);
        return new { rows = bookings.Length, hotelId, roomCount = rooms.Count, firstId = ids[0], lastId = ids[^1], reused = false };
    }

    private static async Task<object> CompareAsync(BookingShardStore store)
    {
        var located = new List<(int Id, string Shard)>();
        foreach (var shard in store.Shards)
        {
            await using var connection = await store.OpenAsync(shard);
            var ids = await connection.QueryAsync<int>($"SELECT \"Id\" FROM public.\"Bookings\" WHERE {DatasetPredicate} ORDER BY \"Id\"");
            located.AddRange(ids.Select(id => (id, shard)));
        }
        if (located.Count == 0 || located.Select(x => x.Id).Distinct().Count() != located.Count)
            throw new InvalidOperationException("Dataset is empty or duplicate IDs exist across shards.");
        if (located.Any(x => store.Router.Route(x.Id) != x.Shard))
            throw new InvalidOperationException("A stored booking does not match the active Router.");
        var keys = located.Select(x => x.Id).Order().ToArray();
        var before = new[] { "shard-0", "shard-1", "shard-2" };
        var after = before.Append("shard-3").ToArray();

        object Experiment(ShardStrategy strategy, int nodes)
        {
            var oldRouter = new ShardRouter(before, strategy, nodes);
            var newRouter = new ShardRouter(after, strategy, nodes);
            var assignments = keys.Select(id => (Id: id, Old: oldRouter.Route(id), New: newRouter.Route(id))).ToArray();
            var moved = assignments.Count(x => x.Old != x.New);
            var oldToOld = assignments.Count(x => x.Old != x.New && x.New != "shard-3");
            if (strategy == ShardStrategy.ConsistentHash && oldToOld != 0)
                throw new InvalidOperationException("Consistent hashing moved keys between old nodes.");
            var five = new ShardRouter(after.Append("shard-4"), strategy, nodes);
            var removed = new ShardRouter(after.Where(s => s != "shard-1"), strategy, nodes);
            return new
            {
                strategy = strategy.ToString(), virtualNodes = nodes, total = keys.Length, moved,
                unchanged = keys.Length - moved, movedPercent = Math.Round(100.0 * moved / keys.Length, 3),
                oldToOld, distributionBefore = before.ToDictionary(s => s, s => assignments.Count(x => x.Old == s)),
                distributionAfter = after.ToDictionary(s => s, s => assignments.Count(x => x.New == s)),
                addedFifthMoved = keys.Count(id => newRouter.Route(id) != five.Route(id)),
                removedShard1Moved = keys.Count(id => newRouter.Route(id) != removed.Route(id)),
                ringBefore = strategy == ShardStrategy.ConsistentHash && nodes == 1
                    ? oldRouter.Ring.Select(n => new { position = n.Position.ToString("X16"), shard = n.Shard }).ToArray() : null,
                ringAfter = strategy == ShardStrategy.ConsistentHash && nodes == 1
                    ? newRouter.Ring.Select(n => new { position = n.Position.ToString("X16"), shard = n.Shard }).ToArray() : null
            };
        }

        return new
        {
            entity = "Bookings", shardKey = "Id", hash = "SHA-256(UTF-8 decimal Id), first 8 bytes, unsigned big-endian",
            activeLayout = store.Router.Fingerprint, total = keys.Length,
            actualCounts = store.Shards.ToDictionary(s => s, s => located.Count(x => x.Shard == s)),
            samples = store.Shards.ToDictionary(s => s, s => located.Where(x => x.Shard == s).Take(3).Select(x => x.Id).ToArray()),
            experiments = new[] { Experiment(ShardStrategy.Modulo, 1), Experiment(ShardStrategy.ConsistentHash, 1),
                Experiment(ShardStrategy.ConsistentHash, 128) },
            note = "3->4 is a routing simulation over actual stored IDs, not a physical data migration."
        };
    }

    private static async Task<object> VerifyAsync(IServiceProvider services, BookingShardStore store)
    {
        var repo = services.GetRequiredService<IBookingRepository>();
        var roomRepo = services.GetRequiredService<IRoomRepository>();
        var hotelRepo = services.GetRequiredService<IHotelRepository>();
        var source = (await store.ReadAsync(DatasetPredicate)).First();
        var loaded = await repo.GetByIdAsync(source.Id) ?? throw new InvalidOperationException("Booking not found via Router.");
        var stats = await services.GetRequiredService<IBookingReportRepository>().GetBookingStatisticsAsync(
            loaded.Room.HotelId, new DateTime(2030, 1, 1), new DateTime(2040, 1, 1));
        if (stats.TotalBookings != 100_000 || stats.ConfirmedBookings != 90_000 || stats.TotalRevenue != 20_000_000)
            throw new InvalidOperationException("Sharded report totals are incorrect.");
        var confirmed = (await store.ReadAsync(DatasetPredicate + " AND \"Status\"='Confirmed'")).First();
        if (await repo.IsRoomAvailableAsync(confirmed.RoomId, confirmed.CheckInDate, confirmed.CheckOutDate))
            throw new InvalidOperationException("Availability ignored a shard.");
        if (!await repo.IsRoomAvailableAsync(confirmed.RoomId, confirmed.CheckInDate, confirmed.CheckOutDate, confirmed.Id))
            throw new InvalidOperationException("Exclude booking ID did not work.");
        var available = await roomRepo.GetAvailableRoomsAsync(loaded.Room.HotelId, confirmed.CheckInDate, confirmed.CheckOutDate);
        if (available.Any(r => r.Id == confirmed.RoomId)) throw new InvalidOperationException("Room search ignored a shard.");
        var roomProtected = false;
        try { await roomRepo.DeleteAsync(confirmed.RoomId); }
        catch (InvalidOperationException) { roomProtected = true; }
        var hotelProtected = false;
        try { await hotelRepo.DeleteAsync(loaded.Room.HotelId); }
        catch (InvalidOperationException) { hotelProtected = true; }
        if (!roomProtected || !hotelProtected) throw new InvalidOperationException("Parent deletion guard failed.");
        return new { passed = true, routedId = source.Id, shard = store.Router.Route(source.Id), stats,
            availability = true, excludeBooking = true, roomSearch = true, roomProtected, hotelProtected };
    }
}
