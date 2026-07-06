using System;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class TrainingReadyProbe
    {
        public TrainingReadyProbeResult Check(SnapshotEnvelope? latestSnapshot, bool bridgeReachable)
        {
            var snapshotAvailable = latestSnapshot is not null;
            var reasons = snapshotAvailable
                ? Array.Empty<string>()
                : new[] { "no_transparent_snapshot_ingested" };

            return new TrainingReadyProbeResult
            {
                Ready = snapshotAvailable && bridgeReachable,
                BackendReachable = true,
                BridgeReachable = bridgeReachable,
                LatestSnapshotAvailable = snapshotAvailable,
                LatestStateHash = latestSnapshot?.StateHash ?? string.Empty,
                SnapshotGameTick = latestSnapshot?.GameTick,
                CheckedAt = DateTimeOffset.UtcNow.ToString("O"),
                BlockReasons = reasons
            };
        }
    }
}
