using System.Threading;
using StardewAI.Contracts.State;

namespace StardewAI.TransparentBridge.State;

internal static class MaterialInventoryGraphCapture
{
    private static readonly AsyncLocal<Capture?> Current = new();

    internal static IDisposable Begin()
    {
        var capture = new Capture(Current.Value);
        Current.Value = capture;
        return capture;
    }

    internal static MaterialInventoryGraph Read(object farm, long playerId, long tick,
        Func<MaterialInventoryGraph> read) => Current.Value is { } capture
            ? capture.Read(farm, playerId, tick, read)
            : read();

    private sealed class Capture : IDisposable
    {
        private readonly Capture? parent;
        private readonly object sync = new();
        private object? cachedFarm;
        private long cachedPlayerId;
        private long cachedTick;
        private MaterialInventoryGraph? graph;
        private bool disposed;

        internal Capture(Capture? parent) => this.parent = parent;

        internal MaterialInventoryGraph Read(object farm, long playerId, long tick,
            Func<MaterialInventoryGraph> read)
        {
            lock (sync)
            {
                if (disposed) return read();
                if (graph is not null && ReferenceEquals(cachedFarm, farm) &&
                    cachedPlayerId == playerId && cachedTick == tick) return graph;
                graph = read();
                cachedFarm = farm;
                cachedPlayerId = playerId;
                cachedTick = tick;
                return graph;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                if (!ReferenceEquals(Current.Value, this))
                    throw new InvalidOperationException("Inventory capture scopes must close in order.");
                Current.Value = parent;
                graph = null;
                cachedFarm = null;
                disposed = true;
            }
        }
    }
}
