using System;

namespace StardewAI.RuntimePrimitives
{
    public interface INativeOutputSnapshotReadApi
    {
        string ReadNativeOutputSnapshotAnchor();
    }

    // SMAPI checks Type.IsPublic, so the exported wrapper must not be nested.
    public sealed class NativeOutputSnapshotReadApi : INativeOutputSnapshotReadApi
    {
        private readonly Func<string> read;

        public NativeOutputSnapshotReadApi(Func<string> read) =>
            this.read = read ?? throw new ArgumentNullException(nameof(read));

        public string ReadNativeOutputSnapshotAnchor() => read();
    }
}
