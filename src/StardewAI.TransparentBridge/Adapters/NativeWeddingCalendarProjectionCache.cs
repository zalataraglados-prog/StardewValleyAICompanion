using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class NativeWeddingCalendarProjectionCache
{
    private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };
    private (ulong SaveId, int Year, string[] Keys, NativeWeddingCalendarProjection Projection)? cached;

    public NativeWeddingCalendarProjection Read(ulong saveId, int year, string[] festivalDateKeys, Func<int, int, bool> nativePredicate)
    {
        if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
        if (festivalDateKeys is null) throw new ArgumentNullException(nameof(festivalDateKeys));
        if (nativePredicate is null) throw new ArgumentNullException(nameof(nativePredicate));
        var keys = festivalDateKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            throw new ArgumentException("Native festival keys must be unique and nonempty.", nameof(festivalDateKeys));
        var previous = cached;
        // These are the exact dependencies of the pinned native predicate: festival key set, year and save seed.
        if (previous.HasValue && previous.Value.SaveId == saveId && previous.Value.Year == year &&
            previous.Value.Keys.SequenceEqual(keys, StringComparer.Ordinal)) return previous.Value.Projection;
        var blocked = Enumerable.Range(0, 4).SelectMany(season => Enumerable.Range(1, 28)
            .Where(day => !nativePredicate(season, day))
            .Select(day => Seasons[season] + day.ToString(CultureInfo.InvariantCulture))).ToArray();
        var projection = new NativeWeddingCalendarProjection(saveId, year, keys, blocked);
        cached = (saveId, year, keys, projection);
        return projection;
    }
}
