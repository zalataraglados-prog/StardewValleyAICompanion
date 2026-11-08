using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    internal static async Task ValidateNoDispatchContextAsync(CropSeedRuntimeOptions options,
        CropSeedRuntimeScopeBinding scope, StrategyCommitmentLedger markerLedger, JsonNode preparedQueue,
        Func<string, string> validateDispatchQueue, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        ValidateTransitionIdentity(options, scope.TotalDay, transition);
        if (transition.TransitionKind != scope.TransitionKind || !string.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), scope.SnapshotPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Crop seed no-dispatch source/kind differs from preparation.");
        var receiptPath = transition.ExecutionReceiptPath;
        var afterSnapshotPath = transition.AfterSnapshotPath;
        var selectedCandidateId = transition.SelectedCandidateId;
        validateDispatchQueue(transition.QueuePath);
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath))!.AsObject();
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(scope.SnapshotPath));
        var after = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(afterSnapshotPath));
        var count = preparedQueue["items"]!.AsArray().Count;
        var stop = receipt["owned_seed_controller_stop"] as JsonObject;
        if (receipt["schema_version"]?.GetValue<string>() != "queue_execution_receipt.v1" ||
            receipt["run_id"]?.GetValue<string>() != options.RunId ||
            receipt["queue_execution_mode"]?.GetValue<string>() != "sequential_queue_items" ||
            receipt["queue_id"]?.GetValue<string>() != preparedQueue["queue_id"]?.GetValue<string>() ||
            string.IsNullOrWhiteSpace(selectedCandidateId) || receipt["selected_candidate_id"]?.GetValue<string>() != selectedCandidateId ||
            receipt["source_state_hash"]?.GetValue<string>() != markerLedger.SourceStateHash ||
            receipt["after_state_hash"]?.GetValue<string>() != markerLedger.SourceStateHash ||
            receipt["executed_item_count"]?.GetValue<int>() != 0 || receipt["planned_item_count"]?.GetValue<int>() != count ||
            receipt["final_pending_item_count"]?.GetValue<int>() != count || receipt["step_results"] is not JsonArray { Count: 0 } ||
            receipt["success"]?.GetValue<bool>() != false || receipt["selected_candidate_completed"]?.GetValue<bool>() != false ||
            receipt["status"]?.GetValue<string>() != "blocked" || receipt["after_snapshot_fresh"]?.GetValue<bool>() != false ||
            receipt["before_game_tick"]?.GetValue<long>() != before?["game_tick"]?.GetValue<long>() ||
            receipt["after_game_tick"]?.GetValue<long>() != before?["game_tick"]?.GetValue<long>() ||
            receipt["max_queue_item_attempts"]?.GetValue<int>() is not (>= 1 and <= StardewAI.Contracts.Training.TeacherEvidenceRolloutLimits.MaxQueueItems) ||
            stop is null || stop["index"]?.GetValue<int>() != 0 ||
            string.IsNullOrWhiteSpace(stop["reason"]?.GetValue<string>()) ||
            stop["native_request_performed"]?.GetValue<bool>() != false ||
            !string.Equals(Path.GetFullPath(stop["current_snapshot_path"]?.GetValue<string>() ?? ""), scope.SnapshotPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !JsonNode.DeepEquals(before, after))
            throw new InvalidDataException("Crop seed no-dispatch receipt is not an unchanged, explicit controller stop.");
        scope.ValidateInputsUnchanged();
    }
}
