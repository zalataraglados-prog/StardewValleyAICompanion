using System.Runtime.CompilerServices;
using System.Threading;

namespace StardewAI.TransparentBridge.State;

internal static class MapActionCapture<T>
{
    private static readonly AsyncLocal<Capture?> Current = new();

    internal static IDisposable Begin()
    {
        var capture = new Capture(Current.Value);
        Current.Value = capture;
        return capture;
    }

    internal static T[] Read(object location, object? map, Func<T[]> read) => Current.Value is { } capture
        ? capture.Read(location, map, read)
        : read();

    private sealed class LocationReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class Capture : IDisposable
    {
        private Capture? parent;
        private readonly object sync = new();
        private readonly Dictionary<object, (object? Map, T[] Rows)> locations = new(new LocationReferenceComparer());
        private bool disposed;

        internal Capture(Capture? parent) => this.parent = parent;

        internal T[] Read(object location, object? map, Func<T[]> read)
        {
            lock (sync)
            {
                if (disposed) return read();
                if (locations.TryGetValue(location, out var cached) && ReferenceEquals(cached.Map, map))
                    return cached.Rows;
                var rows = read();
                locations[location] = (map, rows);
                return rows;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                if (!ReferenceEquals(Current.Value, this))
                    throw new InvalidOperationException("Map action capture scopes must close in order.");
                Current.Value = parent;
                parent = null;
                locations.Clear();
                disposed = true;
            }
        }
    }
}
