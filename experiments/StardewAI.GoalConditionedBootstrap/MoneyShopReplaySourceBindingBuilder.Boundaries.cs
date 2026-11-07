using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyShopReplaySourceBindingBuilder
{
    internal static void VerifySourceFacts(MoneyCounterfactualSourceAudit audit)
    {
        Require(audit.SameActorAndVersion && audit.SameMoneyCheckpointFacts && audit.SameNativeShopContext &&
            audit.MatchingReplayCandidateCount == 1 && audit.ReferencePersistentBoundaryAvailable && audit.ReplayPersistentBoundaryAvailable &&
            audit.ChangedPersistentFields.All(field => field is "environment.training_run_id" or "environment.save_isolation_path"),
            "Money replay source facts, complete persistent boundary or native quote differ.");
    }

    internal static MoneyShopReplayRequestMapping[] BindDenominator(MoneyRevenueCounterfactualProofRequestCatalog reference,
        MoneyRevenueCounterfactualProofRequestCatalog replay, MoneyThresholdNativeOutcomeSelectionBuilder.SelectionInputs before,
        MoneyThresholdNativeOutcomeSelectionBuilder.SelectionInputs after)
    {
        // Compare every original node/candidate, including rejected alternatives; no selected-only projection.
        var originalNodes = Read<JsonElement>(before.CandidateDenominatorPath).GetProperty("nodes");
        var replayNodes = Read<JsonElement>(after.CandidateDenominatorPath).GetProperty("nodes");
        Require(SnapshotHash.CanonicalizeSemantic(originalNodes) == SnapshotHash.CanonicalizeSemantic(replayNodes) &&
            EqualJson(before.Catalog.ComparisonCandidateIds, after.Catalog.ComparisonCandidateIds) &&
            before.Catalog.EnumeratedCandidateCount == after.Catalog.EnumeratedCandidateCount &&
            reference.RequestCount == replay.RequestCount && reference.RequestCount >= 2 &&
            reference.Requests.All(IsDirect) && replay.Requests.All(IsDirect), "Money replay full candidate/request denominator differs or is outside direct-shop scope.");
        var mapping = reference.Requests.OrderBy(row => row.RequestId, StringComparer.Ordinal).Select(row =>
        {
            var matches = replay.Requests.Where(other => EqualJson(row.TerminalCandidate, other.TerminalCandidate) &&
                row.ProofShape == other.ProofShape && row.RequiredRevenueTransitionProofKind == other.RequiredRevenueTransitionProofKind).ToArray();
            Require(matches.Length == 1, "Money replay request mapping is not bijective.");
            return new MoneyShopReplayRequestMapping(row.RequestId, row.RequestSha256, matches[0].RequestId, matches[0].RequestSha256,
                row.TerminalCandidate.CatalogCandidateId);
        }).ToArray();
        Require(mapping.Select(row => row.ReferenceRequestId).Distinct(StringComparer.Ordinal).Count() == reference.RequestCount &&
            mapping.Select(row => row.ReplayRequestId).Distinct(StringComparer.Ordinal).Count() == replay.RequestCount,
            "Money replay request mapping dropped or duplicated an alternative.");
        return mapping;
    }

    private static void VerifyExecutionIdentity(VerifiedNativeExecutionAction native, string isolatedSlot)
    {
        Require(SamePath(Path.GetDirectoryName(Path.GetFullPath(isolatedSlot))!, native.Request.SaveIsolationPath),
            "Money replay isolated slot is not in the recorded native save root.");
        foreach (var snapshot in new[] { native.Before, native.After })
        {
            Require(MoneyRevenueProgressionVerifier.ReadValue(snapshot, "environment", "training_run_id").GetString() == native.Request.RunId &&
                SamePath(MoneyRevenueProgressionVerifier.ReadValue(snapshot, "environment", "save_isolation_path").GetString()!, native.Request.SaveIsolationPath),
                "Money replay execution metadata does not bind its actual raw request.");
        }
    }

    internal static string VerifyInitialLedger(string path, SnapshotEnvelope snapshot)
    {
        var ledger = JsonNode.Parse(Read<JsonElement>(path).GetRawText())!.AsObject();
        var arrays = new[] { "crop_planting_commitments", "material_reservations", "currency_reservations", "machine_relocation_intents", "machine_support_intents", "history" };
        var fields = arrays.Concat(new[] { "schema_version", "ledger_id", "save_id", "player_id", "revision", "updated_at", "source_state_hash" }).ToHashSet(StringComparer.Ordinal);
        Require(ledger.Count == fields.Count && ledger.All(row => fields.Contains(row.Key)) &&
            ledger["schema_version"]?.GetValue<string>() == "strategy_commitment_ledger.v1" && ledger["revision"]?.GetValue<int>() == 0 &&
            ledger["source_state_hash"]?.GetValue<string>() == snapshot.StateHash && ledger["save_id"]?.GetValue<string>() == snapshot.SaveId.Value &&
            ledger["player_id"]?.GetValue<string>() == snapshot.PlayerId.Value && !string.IsNullOrWhiteSpace(ledger["ledger_id"]?.GetValue<string>()) &&
            arrays.All(name => ledger[name] is JsonArray { Count: 0 }),
            "Money replay only supports exact isolated initial ledgers; commitments, history, missing or unknown fields cannot be discarded.");
        _ = Timestamp(ledger["updated_at"]!.GetValue<string>());
        ledger.Remove("updated_at");
        ledger.Remove("source_state_hash");
        return SnapshotHash.CanonicalizeSemantic(JsonSerializer.SerializeToElement(ledger));
    }

    internal static void VerifyExecutables(MoneyShopReplayExecutable[] executables, JsonElement reference, JsonElement replay)
    {
        Require(executables is { Length: 3 } && executables.All(row => row is not null) && executables.Select(row => row.Role).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "backend", "game", "loop" }, StringComparer.Ordinal), "Money replay needs exact game/backend/loop identities.");
        foreach (var executable in executables)
        {
            var path = Path.GetFullPath(executable.Path);
            var expectedName = executable.Role switch { "game" => "Stardew Valley.dll", "backend" => "StardewAI.Backend.dll", _ => "StardewAI.LiveTrainingLoop.dll" };
            Require(Path.GetFileName(path) == expectedName, "Money replay executable role does not name its recorded producer.");
            var current = Hash(path);
            Require(Text(reference.GetProperty("binary_sha256"), path) == current && Text(replay.GetProperty("binary_sha256"), path) == current,
                "Money replay game or execution/compilation producer changed.");
        }
    }
}

internal sealed class MoneyShopReplayMeasurementIdentity
{
    [JsonPropertyName("recorded_before_dispatch_utc")]
    public string RecordedBeforeDispatchUtc { get; set; } = string.Empty;
    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("measured_mod_root_sha256")]
    public string MeasuredModRootSha256 { get; set; } = string.Empty;
    [JsonPropertyName("measured_mod_allowlist")]
    public MoneyThresholdRuntimeModIdentity[] MeasuredModAllowlist { get; set; } = Array.Empty<MoneyThresholdRuntimeModIdentity>();
    [JsonPropertyName("acceptance_scope")]
    public string AcceptanceScope { get; set; } = string.Empty;
    [JsonPropertyName("pre_measurement_native_config_initialization_excluded")]
    public bool PreMeasurementNativeConfigInitializationExcluded { get; set; }
}
