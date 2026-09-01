using System.Diagnostics;

namespace StardewAI.TransparentBridge;

public sealed partial class ModEntry
{
    private const long SlowUpdateGapMicroseconds = 100_000;
    private readonly object snapshotPerformanceLock = new();
    private readonly Dictionary<string, SnapshotBuildMetric> snapshotBuildMetrics =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SnapshotBuildMetric> adapterCollectionMetrics =
        new(StringComparer.OrdinalIgnoreCase);
    private long firstUpdateTimestamp;
    private long lastUpdateTimestamp;
    private long updateGapSamples;
    private long slowUpdateGapCount;
    private long lastUpdateGapMicroseconds;
    private long maxUpdateGapMicroseconds;

    private void RecordUpdateGap()
    {
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref lastUpdateTimestamp, now);
        if (previous <= 0)
        {
            Interlocked.CompareExchange(ref firstUpdateTimestamp, now, 0);
            return;
        }

        var elapsed = ElapsedMicroseconds(previous, now);
        Interlocked.Increment(ref updateGapSamples);
        Interlocked.Exchange(ref lastUpdateGapMicroseconds, elapsed);
        SetMaximum(ref maxUpdateGapMicroseconds, elapsed);
        if (elapsed >= SlowUpdateGapMicroseconds)
        {
            Interlocked.Increment(ref slowUpdateGapCount);
        }
    }

    private void RecordSnapshotBuild(string profile, long startedAt)
    {
        var elapsed = ElapsedMicroseconds(startedAt, Stopwatch.GetTimestamp());
        lock (snapshotPerformanceLock)
        {
            RecordMetric(snapshotBuildMetrics, profile, elapsed);
        }
    }

    private void RecordAdapterCollection(string domain, long elapsedMicroseconds)
    {
        lock (snapshotPerformanceLock)
        {
            RecordMetric(adapterCollectionMetrics, domain, elapsedMicroseconds);
        }
    }

    private object BuildPerformanceResponse()
    {
        object[] snapshotProfiles;
        object[] adapterDomains;
        lock (snapshotPerformanceLock)
        {
            snapshotProfiles = BuildSnapshotMetricRows(snapshotBuildMetrics);
            adapterDomains = BuildAdapterMetricRows(adapterCollectionMetrics);
        }

        var lastTimestamp = Interlocked.Read(ref lastUpdateTimestamp);
        var firstTimestamp = Interlocked.Read(ref firstUpdateTimestamp);
        var samples = Interlocked.Read(ref updateGapSamples);
        var millisecondsSinceLastUpdate = lastTimestamp <= 0
            ? 0d
            : ToMilliseconds(ElapsedMicroseconds(lastTimestamp, Stopwatch.GetTimestamp()));
        var observedUpdatesPerSecond = firstTimestamp <= 0 || lastTimestamp <= firstTimestamp
            ? 0d
            : Math.Round(
                (samples + 1d) * Stopwatch.Frequency /
                (lastTimestamp - firstTimestamp),
                3);
        return new
        {
            schema_version = "transparent_bridge.performance.v1",
            collected_at = DateTimeOffset.UtcNow.ToString("O"),
            game_tick = Interlocked.Read(ref latestGameTick),
            update_loop = new
            {
                sample_count = samples,
                observed_updates_per_second = observedUpdatesPerSecond,
                slow_gap_threshold_ms = ToMilliseconds(SlowUpdateGapMicroseconds),
                slow_gap_count = Interlocked.Read(ref slowUpdateGapCount),
                last_gap_ms = ToMilliseconds(Interlocked.Read(ref lastUpdateGapMicroseconds)),
                max_gap_ms = ToMilliseconds(Interlocked.Read(ref maxUpdateGapMicroseconds)),
                milliseconds_since_last_update = millisecondsSinceLastUpdate
            },
            snapshot_profiles = snapshotProfiles,
            adapter_domains = adapterDomains
        };
    }

    private static void RecordMetric(
        IDictionary<string, SnapshotBuildMetric> metrics,
        string key,
        long elapsedMicroseconds)
    {
        if (!metrics.TryGetValue(key, out var metric))
        {
            metric = new SnapshotBuildMetric();
            metrics[key] = metric;
        }

        metric.Count++;
        metric.TotalMicroseconds += elapsedMicroseconds;
        metric.LastMicroseconds = elapsedMicroseconds;
        metric.MaxMicroseconds = Math.Max(metric.MaxMicroseconds, elapsedMicroseconds);
    }

    private static object[] BuildSnapshotMetricRows(
        IReadOnlyDictionary<string, SnapshotBuildMetric> metrics) =>
        metrics
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => (object)new
            {
                profile = item.Key,
                build_count = item.Value.Count,
                average_build_ms = AverageMilliseconds(item.Value),
                last_build_ms = ToMilliseconds(item.Value.LastMicroseconds),
                max_build_ms = ToMilliseconds(item.Value.MaxMicroseconds)
            })
            .ToArray();

    private static object[] BuildAdapterMetricRows(
        IReadOnlyDictionary<string, SnapshotBuildMetric> metrics) =>
        metrics
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => (object)new
            {
                domain = item.Key,
                collection_count = item.Value.Count,
                average_collection_ms = AverageMilliseconds(item.Value),
                last_collection_ms = ToMilliseconds(item.Value.LastMicroseconds),
                max_collection_ms = ToMilliseconds(item.Value.MaxMicroseconds)
            })
            .ToArray();

    private static double AverageMilliseconds(SnapshotBuildMetric metric) =>
        ToMilliseconds(
            metric.Count == 0
                ? 0
                : metric.TotalMicroseconds / metric.Count);

    private static long ElapsedMicroseconds(long startedAt, long endedAt) =>
        Math.Max(0, (long)((endedAt - startedAt) * 1_000_000d / Stopwatch.Frequency));

    private static double ToMilliseconds(long microseconds) =>
        Math.Round(microseconds / 1000d, 3);

    private static void SetMaximum(ref long target, long candidate)
    {
        var observed = Interlocked.Read(ref target);
        while (candidate > observed)
        {
            var exchanged = Interlocked.CompareExchange(ref target, candidate, observed);
            if (exchanged == observed)
            {
                return;
            }

            observed = exchanged;
        }
    }

    private sealed class SnapshotBuildMetric
    {
        public long Count { get; set; }
        public long TotalMicroseconds { get; set; }
        public long LastMicroseconds { get; set; }
        public long MaxMicroseconds { get; set; }
    }
}
