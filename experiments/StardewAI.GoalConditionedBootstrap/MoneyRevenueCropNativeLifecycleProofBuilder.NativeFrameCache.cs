using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal sealed class NativeLifecycleFrameCache(int capacity = 8)
    {
        private readonly int limit = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        private readonly Dictionary<string, (string FileHash, string Identity)> sources = new(PathComparer);
        private readonly Dictionary<string, (SnapshotEnvelope Snapshot, LinkedListNode<string> Node)> resident = new(StringComparer.Ordinal);
        private readonly LinkedList<string> recency = new();

        internal int ResidentCount => resident.Count;
        internal int PeakResidentCount { get; private set; }

        internal void Bind(string path, SnapshotEnvelope snapshot)
        {
            path = Path.GetFullPath(path);
            var binding = (CurrentTeacherFrontierSupport.HashFile(path), BootstrapArtifactGuard.CanonicalSha256(snapshot, out _));
            Require(!sources.TryGetValue(path, out var previous) || previous == binding,
                "Native lifecycle snapshot source changed while binding its original frame.");
            sources[path] = binding;
            Retain(binding.Item2, snapshot);
        }

        internal SnapshotEnvelope Read(string path)
        {
            path = Path.GetFullPath(path);
            Require(sources.TryGetValue(path, out var source) && CurrentTeacherFrontierSupport.HashFile(path) == source.FileHash,
                "Native lifecycle snapshot source changed after its original capture was loaded.");
            if (resident.TryGetValue(source.Identity, out var entry))
            {
                recency.Remove(entry.Node); recency.AddFirst(entry.Node);
                return entry.Snapshot;
            }
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "Retained native lifecycle snapshot");
            Require(BootstrapArtifactGuard.CanonicalSha256(snapshot, out _) == source.Identity,
                "Reloaded native lifecycle snapshot differs from its complete original frame.");
            Retain(source.Identity, snapshot);
            return snapshot;
        }

        internal void ValidateInputsUnchanged()
        {
            foreach (var (path, source) in sources)
                Require(CurrentTeacherFrontierSupport.HashFile(path) == source.FileHash,
                    "Native lifecycle snapshot source changed during proof assembly.");
        }

        private void Retain(string identity, SnapshotEnvelope snapshot)
        {
            if (resident.TryGetValue(identity, out var existing))
            {
                recency.Remove(existing.Node); recency.AddFirst(existing.Node);
                return;
            }
            if (resident.Count == limit)
            {
                var oldest = recency.Last!;
                resident.Remove(oldest.Value); recency.RemoveLast();
            }
            resident.Add(identity, (snapshot, recency.AddFirst(identity)));
            PeakResidentCount = Math.Max(PeakResidentCount, resident.Count);
        }
    }
}
