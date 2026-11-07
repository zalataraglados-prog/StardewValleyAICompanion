using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    internal async Task<StrategyLedgerEvidence> CompleteWithoutNativeDispatchAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        if (Interlocked.Exchange(ref completionStarted, 1) != 0)
            throw new InvalidOperationException("Crop seed completion was already attempted; recover its original journal.");
        Exception? failure = null;
        try
        {
            ValidateTransitionIdentity(transition);
            if (transition.TransitionKind != scope.TransitionKind || !string.Equals(Path.GetFullPath(transition.BeforeSnapshotPath), scope.SnapshotPath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Crop seed no-dispatch source/kind differs from preparation.");
            var receiptPath = transition.ExecutionReceiptPath;
            var afterSnapshotPath = transition.AfterSnapshotPath;
            var selectedCandidateId = transition.SelectedCandidateId;
            ValidateDispatchQueueCore(transition.QueuePath);
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
                receipt["source_state_hash"]?.GetValue<string>() != marker.Ledger.SourceStateHash ||
                receipt["after_state_hash"]?.GetValue<string>() != marker.Ledger.SourceStateHash ||
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
            var final = await ReadOriginalAuthorityAsync();
            RequireLedger(marker.Ledger, final.Ledger, "no-dispatch readback");
            diagnostic["status"] = "crop_seed_support_prepared_no_native_dispatch";
            diagnostic["native_request_count"] = 0;
            diagnostic["controller_stop_receipt_path"] = Path.GetFullPath(receiptPath);
            diagnostic["final_ledger_path"] = final.Path;
            return final;
        }
        catch (Exception error) { failure = error; diagnostic["status"] = "crop_seed_support_no_dispatch_unverified"; diagnostic["error"] = error.Message; throw; }
        finally { await WriteSummaryAsync(write, Path.Combine(options.EvidenceDirectory, "completion-summary.json"), diagnostic, failure); }
    }
}
