using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal static partial class CropTeacherTerminalSettlementEvidence
{
    internal static async Task<CropTeacherManagedTerminalEvidence> ReadAsync(CropTeacherManagedProductionTrace trace,
        CropTeacherManagedProductionEvidence production, CropTeacherTerminalShippingTarget target, string output)
    {
        // A retained boundary must re-read bytes, not a caller's warm JSON/CAS materialization.
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        output = Path.GetFullPath(output);
        RequireRetainedPath(output, directory: true);
        var outputPaths = new[] { "recurrence-manifest", "recurrence-proof", "terminal-request", "terminal-proof", "terminal-evidence" }
            .Select(name => Path.Combine(output, name + ".json")).ToArray();
        var retained = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        foreach (var path in outputPaths)
        {
            RequireRetainedPath(path, directory: false);
            retained.Pin(path);
        }
        var evidence = retained.Read<JsonObject>(outputPaths[4]);
        trace.ValidateInputsUnchanged();
        var authoritative = NormalizeRetainedHashes(trace.InputHashes);
        var productionPath = Path.GetFullPath(production.ManifestPath);
        RequireRetained(authoritative.TryGetValue(productionPath, out var productionHash) &&
            productionHash == production.ManifestSha256, "production manifest is not pinned by the authoritative trace.");
        var current = new CropSeedRuntimeArtifacts(authoritative);
        var productionManifest = current.Read<JsonObject>(productionPath);
        var productionInputs = ReadRetainedHashes(productionManifest);
        RequireRetained(evidence["schema_version"]?.GetValue<string>() == "crop_teacher_managed_terminal_evidence.v1" &&
            evidence["terminal_settlement_verified"]?.GetValue<bool>() == true &&
            evidence["semantic_saved_content_verified"]?.GetValue<bool>() == false &&
            evidence["formal_product_training_authorized"]?.GetValue<bool>() == false,
            "retained terminal schema/flags differ.");
        RequireRetained(Same(RetainedText(evidence, "native_run_plan_path"), trace.NativeRunPlanPath) &&
            Same(RetainedText(evidence, "accepted_snapshot_path"), trace.AcceptedSnapshotPath) &&
            Same(RetainedText(evidence, "accepted_ledger_path"), trace.AcceptedLedgerPath) &&
            Same(RetainedText(evidence, "production_manifest_path"), productionPath) &&
            RetainedText(evidence, "production_manifest_sha256") == production.ManifestSha256 &&
            JsonNode.DeepEquals(evidence["target"], JsonSerializer.SerializeToNode(target, CropTeacherManagedSeedOwner.JsonOptions)),
            "retained native head, ledger, target or production identity differs.");
        RequireRetained(Same(RetainedText(evidence, "recurrence_manifest_path"), outputPaths[0]) &&
            Same(RetainedText(evidence, "recurrence_proof_path"), outputPaths[1]) &&
            Same(RetainedText(evidence, "terminal_proof_path"), outputPaths[3]), "retained output paths differ.");

        var inputs = ReadRetainedHashes(evidence);
        // The original Emit adds these four outputs before serializing the final manifest.
        // Strip only those exact entries; all other retained inputs must have independent authority.
        foreach (var path in outputPaths.Take(4))
        {
            RequireRetained(inputs.TryGetValue(path, out var hash) && hash == retained.Hashes[path],
                "retained emitted output hash differs: " + path);
            inputs.Remove(path);
        }
        foreach (var (path, hash) in inputs)
        {
            RequireRetained((authoritative.TryGetValue(path, out var traceHash) && traceHash == hash) ||
                (productionInputs.TryGetValue(path, out var proofHash) && proofHash == hash),
                "retained input lacks authoritative trace/production hash: " + path);
            RequireRetainedPath(path, directory: false);
        }
        // Do not add current trace extras (for example later reload evidence) to the old certificate.
        var canonical = new CropTeacherManagedProductionTrace(trace.NativeRunPlanPath, trace.AcceptedSnapshotPath,
            trace.AcceptedLedgerPath, trace.Transitions.ToArray(), inputs);
        canonical.ValidateInputsUnchanged();
        retained.Validate();
        var result = await BuildCoreAsync(canonical, production, target, output,
            (_, _) => throw new InvalidOperationException("Read-only terminal replay cannot write."), readExisting: true).ConfigureAwait(false);
        retained.Validate();
        trace.ValidateInputsUnchanged();
        return result;
    }

    private static Dictionary<string, string> ReadRetainedHashes(JsonObject manifest) => NormalizeRetainedHashes(
        manifest["input_artifact_hashes"]?.Deserialize<Dictionary<string, string>>(CropTeacherManagedSeedOwner.JsonOptions)
        ?? throw new InvalidDataException("Managed terminal replay: missing retained input hashes."));

    private static Dictionary<string, string> NormalizeRetainedHashes(IReadOnlyDictionary<string, string> hashes)
    {
        var result = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var (path, hash) in hashes)
        {
            RequireRetained(Path.IsPathFullyQualified(path), "retained artifact path is not absolute.");
            RequireRetained(result.TryAdd(Path.GetFullPath(path), hash), "retained artifact paths alias one another.");
        }
        return result;
    }

    private static string RetainedText(JsonObject node, string name)
    {
        var value = node[name]?.GetValue<string>() ?? throw new InvalidDataException("Managed terminal replay: missing " + name);
        if (name.EndsWith("_path", StringComparison.Ordinal))
            RequireRetained(Path.IsPathFullyQualified(value), "retained path is not absolute: " + name);
        return value;
    }

    private static void RequireRetainedPath(string path, bool directory)
    {
        RequireRetained(directory ? Directory.Exists(path) : File.Exists(path), "missing or nonregular retained output/input: " + path);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            RequireRetained((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "retained path crosses a reparse point.");
    }

    private static void RequireRetained(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Managed terminal replay: " + reason);
    }
}
