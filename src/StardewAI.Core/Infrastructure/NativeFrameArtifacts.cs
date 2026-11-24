using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

// The original Bootstrap artifact operations, shared by its frame reader and
// the Core admission consumer. Snapshot chunk/hash verification remains in the
// existing content-addressed reader.
internal static class NativeFrameArtifacts
{
    internal static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static T Read<T>(string path, string label)
    {
        var text = ContentAddressedJsonArtifactReader.ReadAllText(path);
        using var operation = ValidationTraceSession.BeginOperation("json_deserialize",
            "StardewAI.GoalConditionedBootstrap.CurrentTeacherFrontierSupport", "v1", path);
        var clock = operation.IsEnabled ? Stopwatch.StartNew() : null;
        try { return JsonSerializer.Deserialize<T>(text, Options) ?? throw new InvalidDataException(label + " is null."); }
        finally { clock?.Stop(); if (clock is not null) operation.AddJsonParseMilliseconds(clock.Elapsed.TotalMilliseconds); }
    }

    internal static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var field) || field.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(field.GetString())) throw new InvalidDataException($"Snapshot property {property} is missing.");
        return field.GetString()!;
    }

    internal static string HashFile(string path)
    {
        using var operation = ValidationTraceSession.BeginOperation("file_hash",
            "StardewAI.GoalConditionedBootstrap.CurrentTeacherFrontierSupport", "v2", path);
        var hash = InvocationReadSession.HashFile(path, out var observation);
        operation.AddBytesRead(observation.PhysicalBytesRead);
        operation.AddHashBytes(observation.HashBytes);
        if (observation.CacheHit) operation.MarkCacheHit();
        operation.AddArtifactId(hash);
        return hash;
    }

    internal static string HashText(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
}
