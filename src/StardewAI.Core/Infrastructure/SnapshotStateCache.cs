using System;
using System.Runtime.CompilerServices;
using System.Threading;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure
{
    internal sealed class SnapshotStateCache<TValue>
        where TValue : class
    {
        private readonly ConditionalWeakTable<SnapshotEnvelope, Entry>
            entries = new();
        private int buildCount;

        internal int BuildCount => Volatile.Read(ref buildCount);

        internal TValue GetValue(
            SnapshotEnvelope snapshot,
            Func<SnapshotEnvelope, TValue> factory)
        {
            if (string.IsNullOrWhiteSpace(snapshot.StateHash))
                return Build(snapshot, factory, expectedStateHash: null);

            var entry = entries.GetValue(snapshot, static _ => new Entry());
            lock (entry)
            {
                var stateHash = snapshot.StateHash;
                if (entry.Value is not null &&
                    string.Equals(
                        entry.StateHash,
                        stateHash,
                        StringComparison.Ordinal))
                {
                    return entry.Value;
                }

                var value = Build(snapshot, factory, stateHash);
                entry.StateHash = stateHash;
                entry.Value = value;
                return value;
            }
        }

        private TValue Build(
            SnapshotEnvelope snapshot,
            Func<SnapshotEnvelope, TValue> factory,
            string? expectedStateHash)
        {
            var value = factory(snapshot) ??
                throw new InvalidOperationException(
                    "Snapshot-state cache factory returned null.");
            if (expectedStateHash is not null &&
                !string.Equals(
                    expectedStateHash,
                    snapshot.StateHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Snapshot StateHash changed while building a derived cache value.");
            }

            Interlocked.Increment(ref buildCount);
            return value;
        }

        private sealed class Entry
        {
            public string StateHash { get; set; } = string.Empty;

            public TValue? Value { get; set; }
        }
    }
}
