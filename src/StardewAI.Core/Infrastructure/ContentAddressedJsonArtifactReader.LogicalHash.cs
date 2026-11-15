using System;
using System.IO;
using System.Security.Cryptography;

namespace StardewAI.Core.Infrastructure;

public static partial class ContentAddressedJsonArtifactReader
{
    // Durable identity checks must reread disk even inside a caller's warm session.
    public static string ComputeLogicalSha256(string logicalPath)
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        using var operation = ValidationTraceSession.BeginOperation(
            "logical_artifact_hash", TraceProducerId, TraceProducerVersion, logicalPath);
        var stored = InvocationReadSession.ReadAllText(logicalPath, Utf8WithoutBom, out var observation);
        operation.AddBytesRead(observation.PhysicalBytesRead);
        operation.AddHashBytes(observation.HashBytes);
        _ = MaterializeIfNeeded(logicalPath, stored, operation, out var logicalSha256, hashOnly: true);
        if (logicalSha256 is not null) return logicalSha256;

        var bytes = Utf8WithoutBom.GetBytes(stored);
        operation.AddHashBytes(bytes.LongLength);
        var hash = Sha256(bytes);
        operation.AddArtifactId(hash);
        return hash;
    }

    private sealed class LogicalHashStream : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public string Finish() => FormatHash(hash.GetHashAndReset());
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => hash.AppendData(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => hash.AppendData(buffer);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) hash.Dispose();
            base.Dispose(disposing);
        }
    }
}
