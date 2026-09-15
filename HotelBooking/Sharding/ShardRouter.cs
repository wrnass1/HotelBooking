using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HotelBooking.Sharding;

public enum ShardStrategy { Modulo, ConsistentHash }

// Stable across processes and machines; never use string.GetHashCode for persisted routing.
public sealed class ShardRouter
{
    private readonly string[] _shards;
    private readonly (ulong Position, string Shard)[] _ring;
    public ShardStrategy Strategy { get; }
    public string Fingerprint { get; }

    public ShardRouter(IEnumerable<string> shards, ShardStrategy strategy, int virtualNodes = 1)
    {
        _shards = shards.Order(StringComparer.Ordinal).ToArray();
        if (_shards.Length == 0 || _shards.Any(string.IsNullOrWhiteSpace) ||
            _shards.Distinct(StringComparer.Ordinal).Count() != _shards.Length)
            throw new ArgumentException("Shard names must be nonempty and unique.");
        if (virtualNodes < 1 || virtualNodes > 4096 || !Enum.IsDefined(strategy))
            throw new ArgumentOutOfRangeException(nameof(virtualNodes));
        Strategy = strategy;
        var nodes = strategy == ShardStrategy.Modulo ? 1 : virtualNodes;
        Fingerprint = $"sha256-u64be-v1|{strategy}|{nodes}|{string.Join(',', _shards)}";
        _ring = _shards.SelectMany(shard => Enumerable.Range(0, nodes)
                .Select(i => (Position: Hash($"{shard}:{i.ToString(CultureInfo.InvariantCulture)}"), Shard: shard)))
            .OrderBy(node => node.Position).ThenBy(node => node.Shard, StringComparer.Ordinal).ToArray();
    }

    public static ulong Hash(string value) => BinaryPrimitives.ReadUInt64BigEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public string Route(int bookingId)
    {
        if (bookingId <= 0) throw new ArgumentOutOfRangeException(nameof(bookingId));
        return RouteHash(Hash(bookingId.ToString(CultureInfo.InvariantCulture)));
    }

    public string RouteHash(ulong hash)
    {
        if (Strategy == ShardStrategy.Modulo) return _shards[(int)(hash % (ulong)_shards.Length)];
        // First node clockwise, including equality; wrap at the end of the ring.
        var low = 0;
        var high = _ring.Length;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (_ring[mid].Position < hash) low = mid + 1;
            else high = mid;
        }
        return _ring[low == _ring.Length ? 0 : low].Shard;
    }

    public IReadOnlyList<(ulong Position, string Shard)> Ring => Array.AsReadOnly(_ring);
}
