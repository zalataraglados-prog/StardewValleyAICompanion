using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class NativeWeddingCalendarProjectionTests
{
    [Fact]
    public void ActualProducerCacheCallsEveryNativeDateOnceAndDoesNotPollOnSnapshotTicks()
    {
        var cache = new NativeWeddingCalendarProjectionCache();
        var calls = new List<(int Season, int Day)>();
        bool Native(int season, int day)
        {
            calls.Add((season, day));
            return !((season == 0 && day == 13) || (season == 1 && day == 6));
        }
        var projection = cache.Read(442159967, 2, new[] { "spring13" }, Native);
        Assert.Equal(112, calls.Count);
        Assert.Equal(112, calls.Distinct().Count());
        Assert.Equal(new[] { "spring13", "summer6" }, projection.BlockedDateKeys);
        Assert.Equal("Game1.canHaveWeddingOnDay", projection.NativePredicate);
        Assert.Equal(112, projection.EvaluatedDateCount);
        for (var snapshot = 0; snapshot < 20; snapshot++)
            Assert.Same(projection, cache.Read(442159967, 2, new[] { "spring13" }, Native));
        Assert.Equal(112, calls.Count);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("year")]
    [InlineData("festival_keys")]
    public void EveryActualNativeDependencyInvalidatesTheCachedProjection(string mutation)
    {
        var cache = new NativeWeddingCalendarProjectionCache();
        var calls = 0;
        bool Native(int _, int __) { calls++; return true; }
        var first = cache.Read(1, 2, new[] { "spring13" }, Native);
        var next = cache.Read(mutation == "seed" ? 2UL : 1UL, mutation == "year" ? 3 : 2,
            mutation == "festival_keys" ? new[] { "spring13", "fall16" } : new[] { "spring13" }, Native);
        Assert.NotSame(first, next);
        Assert.Equal(224, calls);
    }

    [Fact]
    public void CallerArraysAndSerializationCannotCorruptTheProducerOrLoseUnsignedSeedBits()
    {
        var keys = new[] { "winter25", "spring13" };
        var cache = new NativeWeddingCalendarProjectionCache();
        var result = cache.Read(ulong.MaxValue, 2, keys, (season, day) => season != 0 || day != 13);
        keys[0] = "bad";
        result.FestivalDateKeys[0] = "bad";
        result.BlockedDateKeys[0] = "bad";
        Assert.Same(result, cache.Read(ulong.MaxValue, 2, new[] { "spring13", "winter25" }, (_, _) => throw new Exception("Unexpected rebuild")));
        Assert.Equal(new[] { "spring13", "winter25" }, result.FestivalDateKeys);
        Assert.Equal(new[] { "spring13" }, result.BlockedDateKeys);
        var json = JsonSerializer.Serialize(result);
        var replay = JsonSerializer.Deserialize<NativeWeddingCalendarProjection>(json)!;
        Assert.Equal(ulong.MaxValue, replay.SaveUniqueId);
        Assert.Equal(json, JsonSerializer.Serialize(replay));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void InvalidKeysOrYearCannotPoisonAPreviouslyValidCache(int year, bool duplicate)
    {
        var cache = new NativeWeddingCalendarProjectionCache();
        var first = cache.Read(1, 2, Array.Empty<string>(), (_, _) => true);
        Assert.ThrowsAny<ArgumentException>(() => cache.Read(1, year,
            duplicate ? new[] { "spring13", "spring13" } : Array.Empty<string>(), (_, _) => throw new Exception("Should not evaluate")));
        Assert.Same(first, cache.Read(1, 2, Array.Empty<string>(), (_, _) => throw new Exception("Should remain cached")));
    }
}
