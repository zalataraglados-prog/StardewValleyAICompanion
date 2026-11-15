using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private IReadOnlySet<string> ReadAcceptedDailyCompletions(
        (int Sequence, string Path, string Hash, JsonObject Certificate)[] history, int anchor,
        string sourcePath, string planPath, string rankingPath, string[] owned, SelectedQueueDecisionLease lease)
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in history.Where(row => row.Sequence > anchor &&
            row.Certificate["operation_kind"]?.GetValue<string>() == "shared_source_return"))
        {
            var admissionPath = RecordPath(row.Sequence, "intent");
            var intent = ReadBoundPlanningRecord(admissionPath, Text(row.Certificate, "admission_sha256"));
            if (intent["shared_source_execution_protocol"]?.GetValue<string>() is not "selected_work_yield.v1" and not "selected_work_yield.v2") continue;
            var selection = SharedSourceSelection(intent);
            if (!SamePath(selection.DecisionSnapshotPath, sourcePath) || !SamePath(selection.DecisionPlanPath, planPath) ||
                !SamePath(selection.DecisionRankingPath, rankingPath)) continue;
            var selected = lease.CandidateAt(selection.SelectedQueueIndex);
            if (row.Certificate["native_shared_source_return_verified"]?.GetValue<bool>() != true ||
                owned.Contains(selected.CandidateId, StringComparer.Ordinal) ||
                !selection.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(owned.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidDataException("Accepted daily completion changed original decision ownership.");
            var transition = intent["shared_source_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
            var resultPath = Path.Combine(StoredPath(intent, "runtime_directory"), "shared-source-native-result.json");
            var result = ReadBoundPlanningRecord(resultPath, Text(row.Certificate, "shared-source_native_result_sha256"));
            var hashes = new Dictionary<string, string>(result["input_artifact_hashes"]!
                .Deserialize<Dictionary<string, string>>(JsonOptions)!, PathComparer);
            if (Text(result, "schema_version") != "crop_teacher_owned_shared-source_native_result.v1" ||
                result["native_outcome_verified"]?.GetValue<bool>() != true ||
                result["formal_product_training_authorized"]?.GetValue<bool>() != false ||
                !hashes.TryGetValue(Path.GetFullPath(transition.ExecutionReceiptPath), out var receiptHash))
                throw new InvalidDataException("Accepted daily completion lacks its original sealed native outcome.");
            var receipt = ReadBoundPlanningRecord(transition.ExecutionReceiptPath, receiptHash).Deserialize<QueueExecutionReceiptEnvelope>(JsonOptions)!;
            if (receipt.SelectedCandidateId != selected.CandidateId || transition.SelectedCandidateId != selected.CandidateId ||
                receipt.AfterStateHash != Text(row.Certificate, "shared_after_state_hash"))
                throw new InvalidDataException("Accepted daily completion belongs to another candidate or source.");
            if (receipt.SelectedCandidateCompleted && !completed.Add(selected.CandidateId))
                throw new InvalidDataException("Accepted daily decision completes the same selected candidate twice.");
        }
        return completed;
    }

    private static JsonObject ReadBoundPlanningRecord(string path, string hash)
    {
        if (HashFile(path) != hash) throw new InvalidDataException("Accepted daily planning input changed.");
        var value = Read<JsonObject>(path);
        if (HashFile(path) != hash) throw new InvalidDataException("Accepted daily planning input changed while reading.");
        return value;
    }

    // An explicit persisted-decision reference may bridge a reset game clock ONLY through this
    // owner's already accepted saved-content readback. It grants no dispatch/retry permission.
    private bool ValidateRetainedDailySelection(CropTeacherSharedSourceSelection selection, SnapshotEnvelope original, SnapshotEnvelope current,
        string candidateId)
    {
        var history = ReadAcceptedPlanningCertificates().ToArray();
        var retained = ReadDailyDecisionReference(selection.DecisionReference!, history);
        if (!SamePath(retained.SourceSnapshotPath, selection.DecisionSnapshotPath) ||
            !SamePath(retained.PlanPath, selection.DecisionPlanPath) || !SamePath(retained.RankingPath, selection.DecisionRankingPath) ||
            !retained.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(selection.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal)) ||
            original.GameVersion != current.GameVersion || SnapshotInt(current, "time", "total_days") != retained.TotalDay)
            throw new InvalidDataException("Selected foreign continuation is not this original persisted daily decision.");
        if (retained.CompletedForeignCandidateIds.Contains(candidateId))
            throw new InvalidDataException("Persisted daily candidate already has an accepted original native completion.");
        var value = ReadBoundPlanningRecord(retained.Reference.Path, retained.Reference.Sha256);
        var anchor = value["owner_anchor_sequence"]!.GetValue<int>();
        var suffix = history.Where(row => row.Sequence > anchor).ToArray();
        if (suffix.Length == 0 || suffix[0].Certificate["operation_kind"]?.GetValue<string>() != "saved_content_readback") return false;
        var reloads = suffix.Where(row => row.Certificate["operation_kind"]?.GetValue<string>() == "saved_content_readback").ToArray();
        foreach (var row in reloads)
            if (row.Certificate["native_reload_verified"]?.GetValue<bool>() != true ||
                row.Certificate["native_saved_content_readback_verified"]?.GetValue<bool>() != true)
                throw new InvalidDataException("Daily decision clock continuation lacks an accepted native readback.");
        var latest = reloads[^1].Certificate;
        var loaded = Read<SnapshotEnvelope>(StoredPath(latest, "after_snapshot_path"));
        if (HashFile(StoredPath(latest, "after_snapshot_path")) != Text(latest, "after_snapshot_sha256") ||
            loaded.SaveId.Value != current.SaveId.Value || loaded.PlayerId.Value != current.PlayerId.Value ||
            loaded.GameVersion != current.GameVersion || SnapshotInt(loaded, "time", "total_days") != retained.TotalDay || current.GameTick < loaded.GameTick)
            throw new InvalidDataException("Daily decision does not continue its accepted native load epoch.");
        return true;
    }
}
