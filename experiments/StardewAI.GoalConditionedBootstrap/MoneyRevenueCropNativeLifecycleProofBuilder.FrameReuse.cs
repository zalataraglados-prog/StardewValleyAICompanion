using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal sealed class LifecycleFramePool
    {
        private readonly Dictionary<string, SnapshotEnvelope> frames = new(StringComparer.Ordinal);

        internal SnapshotEnvelope Share(SnapshotEnvelope snapshot)
        {
            // Hash the entire loaded envelope, never its claimed state hash or CAS index.
            var identity = BootstrapArtifactGuard.CanonicalSha256(snapshot, out _);
            if (frames.TryGetValue(identity, out var existing))
                return existing;
            frames.Add(identity, snapshot);
            return snapshot;
        }
    }
}
