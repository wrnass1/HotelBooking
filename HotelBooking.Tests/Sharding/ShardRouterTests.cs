using HotelBooking.Sharding;
using Xunit;

namespace HotelBooking.Tests.Sharding;

public class ShardRouterTests
{
    private static readonly string[] Three = ["shard-0", "shard-1", "shard-2"];

    [Fact]
    public void HashHasStablePublishedSha256Prefix()
        => Assert.Equal(0x6b86b273ff34fce1UL, ShardRouter.Hash("1"));

    [Theory]
    [InlineData(ShardStrategy.Modulo, 1)]
    [InlineData(ShardStrategy.ConsistentHash, 1)]
    [InlineData(ShardStrategy.ConsistentHash, 128)]
    public void ReorderingConfigurationDoesNotMoveKeys(ShardStrategy strategy, int nodes)
    {
        var first = new ShardRouter(Three, strategy, nodes);
        var second = new ShardRouter(Three.Reverse(), strategy, nodes);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        foreach (var id in Enumerable.Range(1, 10_000)) Assert.Equal(first.Route(id), second.Route(id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public void AddingNodeOnlyMovesKeysToNewNode(int nodes)
    {
        var old = new ShardRouter(Three, ShardStrategy.ConsistentHash, nodes);
        var added = new ShardRouter(Three.Append("shard-3"), ShardStrategy.ConsistentHash, nodes);
        var moved = 0;
        foreach (var id in Enumerable.Range(1, 10_000))
            if (old.Route(id) != added.Route(id)) { Assert.Equal("shard-3", added.Route(id)); moved++; }
        Assert.InRange(moved, 1, 9_999);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public void RemovingNodeKeepsAllOtherAssignments(int nodes)
    {
        var old = new ShardRouter(Three, ShardStrategy.ConsistentHash, nodes);
        var removed = new ShardRouter(Three.Where(s => s != "shard-1"), ShardStrategy.ConsistentHash, nodes);
        foreach (var id in Enumerable.Range(1, 10_000))
            if (old.Route(id) != "shard-1") Assert.Equal(old.Route(id), removed.Route(id));
    }

    [Fact]
    public void RingIncludesBoundaryAndWrapsAtMaximum()
    {
        var router = new ShardRouter(Three, ShardStrategy.ConsistentHash);
        foreach (var node in router.Ring) Assert.Equal(node.Shard, router.RouteHash(node.Position));
        Assert.Equal(router.Ring[0].Shard, router.RouteHash(ulong.MaxValue));
        Assert.Equal(router.Ring[0].Shard, router.RouteHash(0));
    }

    [Fact]
    public void ModuloUsesUnsignedHashAndShardCount()
    {
        var router = new ShardRouter(Three, ShardStrategy.Modulo);
        Assert.Equal(Three[(int)(ulong.MaxValue % 3)], router.RouteHash(ulong.MaxValue));
        Assert.Equal("shard-0", router.RouteHash(0));
    }

    [Fact]
    public void InvalidConfigurationAndKeysAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ShardRouter([], ShardStrategy.Modulo));
        Assert.Throws<ArgumentException>(() => new ShardRouter(["same", "same"], ShardStrategy.Modulo));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShardRouter(Three, ShardStrategy.ConsistentHash, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShardRouter(Three, ShardStrategy.Modulo).Route(0));
    }
}
