using System;
using System.Collections.Generic;

namespace StardewAI.RuntimePrimitives
{
    public sealed class BoundedNativeObservationBuffer<T>
    {
        private readonly List<T> entries = new List<T>();
        public BoundedNativeObservationBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }
        public int Capacity { get; }
        public bool Overflowed { get; private set; }
        public bool TryAdd(T value)
        {
            if (entries.Count >= Capacity) { Overflowed = true; return false; }
            entries.Add(value);
            return true;
        }
        public T[] Snapshot() => entries.ToArray();
    }

}
