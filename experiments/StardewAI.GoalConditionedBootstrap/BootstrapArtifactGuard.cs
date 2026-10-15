global using static StardewAI.GoalConditionedBootstrap.BootstrapArtifactGuard;

using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static class BootstrapArtifactGuard
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    public static bool EqualJson(object? left, object? right)
    {
        using var operation = ValidationTraceSession.BeginOperation(
            "canonical_artifact_equality",
            "BootstrapArtifactGuard.EqualJson",
            "v2");
        var leftHash = CanonicalSha256(left, out var leftBytes);
        var rightHash = CanonicalSha256(right, out var rightBytes);
        operation.AddHashBytes(leftBytes + rightBytes);
        return string.Equals(leftHash, rightHash, StringComparison.Ordinal);
    }

    public static string CanonicalSha256(object? value, out long serializedBytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new HashingWriteStream(hash);
        JsonSerializer.Serialize(
            stream,
            value,
            value?.GetType() ?? typeof(object),
            JsonDefaults.Compact);
        serializedBytes = stream.BytesWritten;
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsSha256(string value) => IsLowerSha256(value);

    private sealed class HashingWriteStream : Stream
    {
        private readonly IncrementalHash hash;

        public HashingWriteStream(IncrementalHash hash) => this.hash = hash;

        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            hash.AppendData(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            hash.AppendData(buffer);
            BytesWritten += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();
    }
}
