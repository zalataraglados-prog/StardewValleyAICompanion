using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class CropTeacherAcceptedSavedReadbackVerifier
{
    public static CropTeacherAcceptedSavedReadbackBoundary Verify(string previousAfterPath, string resumedBeforePath,
        string runId, IReadOnlyList<MoneyRevenueCropAcceptedSavedReadbackReference> references)
    {
        Require(references.Count > 0, "Accepted saved readback references are missing.");
        var source = Read<SnapshotEnvelope>(previousAfterPath);
        var current = source; var currentPath = previousAfterPath; var sequence = 0;
        var hashes = new List<string>();
        foreach (var reference in references)
        {
            Require(reference.OwnerSequence > sequence && reference.InputArtifactHashes.Count > 0 &&
                    reference.InputArtifactHashes.Keys.All(Path.IsPathFullyQualified), "Accepted readback order/input paths changed.");
            sequence = reference.OwnerSequence;
            var actual = ReadBoundHashes(reference.InputArtifactHashes.Keys);
            Require(actual.Count == reference.InputArtifactHashes.Count && reference.InputArtifactHashes.All(pair =>
                actual.TryGetValue(Path.GetFullPath(pair.Key), out var hash) && hash == pair.Value), "Accepted readback input hashes changed.");
            // Only JSON inputs use logical CAS validation; native logs and executables retain byte hashes.
            var inputs = new CropTeacherRetainedInputs(actual.Keys.Where(path =>
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray());
            string Pin(string path)
            {
                Require(Path.IsPathFullyQualified(path) && actual.ContainsKey(Path.GetFullPath(path)), "Accepted readback omitted a bound input.");
                return actual[Path.GetFullPath(path)];
            }
            var acceptanceHash = Pin(reference.AcceptancePath);
            var accepted = Read<JsonElement>(reference.AcceptancePath);
            var admission = Read<JsonElement>(reference.AdmissionPath);
            var observation = Read<JsonElement>(reference.ObservationPath);
            Require(Text(accepted, "schema_version") == "crop_teacher_managed_seed_acceptance.v1" &&
                    accepted.GetProperty("sequence").GetInt32() == reference.OwnerSequence &&
                    Text(accepted, "operation_kind") == "saved_content_readback" && Text(admission, "operation_kind") == "saved_content_readback" &&
                    Text(accepted, "run_id") == runId && Text(admission, "run_id") == runId &&
                    accepted.GetProperty("native_saved_content_readback_verified").GetBoolean() &&
                    accepted.GetProperty("native_reload_verified").GetBoolean() && !accepted.GetProperty("formal_product_training_authorized").GetBoolean() &&
                    Text(accepted, "admission_sha256") == Pin(reference.AdmissionPath) &&
                    Text(accepted, "reload_observation_sha256") == Pin(reference.ObservationPath) &&
                    Same(Text(admission, "before_snapshot_path"), currentPath) &&
                    Same(Text(admission, "reload_observation_path"), reference.ObservationPath), "Accepted readback lost its original owner admission.");
            foreach (var pair in admission.GetProperty("input_artifact_hashes").EnumerateObject())
                Require(Pin(pair.Name) == pair.Value.GetString(), "Accepted readback admission inputs changed.");
            Require(Text(observation, "schemaVersion") == "crop_teacher_native_reload_observation.v1" &&
                    Text(observation, "runId") == runId && Same(Text(observation, "sourceSnapshotPath"), currentPath),
                "Accepted readback observation has another source/run.");
            foreach (var pair in observation.GetProperty("inputArtifactHashes").EnumerateObject())
                Require(Pin(pair.Name) == pair.Value.GetString(), "Accepted readback observation inputs changed.");
            var loadedPath = Text(accepted, "after_snapshot_path");
            Require(Same(Text(observation, "loadedSnapshotPath"), loadedPath) &&
                    Text(accepted, "after_snapshot_sha256") == Pin(loadedPath), "Accepted readback loaded capture changed.");
            var savedPath = Text(observation, "overnightSaveBoundaryPath");
            Require(Text(accepted, "saved_content_boundary_sha256") == Pin(savedPath), "Accepted readback saved boundary changed.");
            var saved = Read<JsonElement>(savedPath);
            Require(Text(saved, "schema_version") == "crop_teacher_owned_overnight_save_boundary.v1" &&
                    saved.GetProperty("native_save_boundary_verified").GetBoolean() && !saved.GetProperty("formal_product_training_authorized").GetBoolean() &&
                    Text(saved, "run_id") == runId && Text(saved, "save_id") == source.SaveId.Value &&
                    Text(saved, "player_id") == source.PlayerId.Value && Same(Text(saved, "after_snapshot_path"), previousAfterPath) &&
                    BootstrapArtifactGuard.EqualJson(saved.GetProperty("after_save"), observation.GetProperty("beforeReadSave")) &&
                    BootstrapArtifactGuard.EqualJson(saved.GetProperty("after_save"), observation.GetProperty("afterReadSave")),
                "Accepted readback does not bind the original native save.");
            _ = Pin(previousAfterPath); _ = Pin(currentPath);
            var loaded = Read<SnapshotEnvelope>(loadedPath);
            Require(Text(accepted, "after_state_hash") == loaded.StateHash, "Accepted readback head changed.");
            // Native provenance is reused from the sealed owner acceptance; semantic comparison uses the ORIGINAL checker.
            CropTeacherSavedResourceReadback.VerifyNativeLoadSnapshotContent(source, loaded);
            if (!Same(currentPath, previousAfterPath))
                CropTeacherSavedResourceReadback.VerifyNativeLoadSnapshotContent(current, loaded);
            inputs.Validate();
            var finalHashes = ReadBoundHashes(actual.Keys);
            Require(actual.All(pair => finalHashes[pair.Key] == pair.Value), "Accepted readback inputs changed during validation.");
            hashes.Add(acceptanceHash); current = loaded; currentPath = loadedPath;
        }
        var resumed = Read<SnapshotEnvelope>(resumedBeforePath);
        Require(current.StateHash == resumed.StateHash && current.GameTick == resumed.GameTick &&
                BootstrapArtifactGuard.EqualJson(current, resumed), "Accepted readback does not reach the next original native source.");
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(source, out var boundary), "Accepted readback persisted boundary is unavailable.");
        var json = JsonSerializer.SerializeToElement(boundary, JsonDefaults.Options);
        var hashValue = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SnapshotHash.Canonicalize(json)))).ToLowerInvariant();
        return new(hashValue, hashes.ToArray());
    }

    private static Dictionary<string, string> ReadBoundHashes(IEnumerable<string> paths)
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        var result = new Dictionary<string, string>(CropTeacherSeedTransferNativeOutcome.PathComparer);
        foreach (var path in paths)
        {
            var full = Path.GetFullPath(path);
            Require(File.Exists(full), "Accepted readback input is not a regular file.");
            for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                    "Accepted readback inputs must not cross reparse points.");
            Require(result.TryAdd(full, CurrentTeacherFrontierSupport.HashFile(full)), "Accepted readback input paths are duplicated.");
        }
        return result;
    }

    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "Accepted saved readback");
    private static string Text(JsonElement node, string name) => CurrentTeacherFrontierSupport.RequiredString(node, name);
    private static bool Same(string a, string b) => Path.GetFullPath(a).Equals(Path.GetFullPath(b),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
