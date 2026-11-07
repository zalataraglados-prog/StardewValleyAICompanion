using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyAppliedSeedAcquisitionProgress(
        string root,
        string planPath,
        MoneyRevenueCropSeedAcquisitionExecutionManifest manifest,
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions)
    {
        var modern = transitions.Select((transition, index) =>
        {
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                transition.QueuePath, "progress fixture queue");
            var raw = JsonNode.Parse(File.ReadAllText(
                transition.ExecutionReceiptPath))!.AsObject();
            raw["status"] = "applied";
            raw["success"] = false;
            raw["selected_candidate_completed"] = false;
            raw["queue_progress_verified"] = true;
            raw["primitive_verification_status"] = "verified";
            raw["selected_queue_continuation_retry_next_iteration"] = true;
            raw["queue_replan_stop_reason"] =
                "selected_queue_continuation_retry_next_iteration";
            raw["selected_queue_decision_complete"] = false;
            raw["selected_queue_resume_index"] = 0;
            var continuation = new JsonObject
            {
                ["kind"] = "economy_purchase",
                ["target_location"] = "SeedShop"
            };
            foreach (var parameter in queue.Items[0].NormalizedCommand.Parameters
                         .Where(value => value.Name.StartsWith(
                             "continuation.", StringComparison.Ordinal)))
                continuation[parameter.Name["continuation.".Length..]] =
                    parameter.Value;
            var purchased = index > 0;
            raw["objective_continuation"] = continuation.DeepClone();
            raw["objective_continuation_completed"] = purchased;
            raw["completed_objective_continuations"] = purchased
                ? new JsonArray(continuation.DeepClone())
                : new JsonArray();
            foreach (var step in raw["step_results"]!.AsArray())
                step!["selected_queue_candidate_completed"] = false;
            var path = Path.Combine(root, $"seed-progress-{index}-receipt.json");
            Write(path, raw);
            var changed = transition with { ExecutionReceiptPath = path };
            Require(ValidateSeedProgressFixture(changed, allowProgress: false)
                    .Length > 0,
                "Default queue validation admitted incomplete applied progress.");
            Require(ValidateSeedProgressFixture(changed, allowProgress: true)
                    .Length == 0,
                "Opted-in seed progress lost verified native steps.");
            return changed;
        }).ToArray();

        var manifestPath = Path.Combine(root, "seed-progress-manifest.json");
        Write(manifestPath, new MoneyRevenueCropSeedAcquisitionExecutionManifest
        {
            CropProductionPlanPath = manifest.CropProductionPlanPath,
            CropProductionPlanSha256 = manifest.CropProductionPlanSha256,
            CommitmentId = manifest.CommitmentId,
            SourceStateHash = manifest.SourceStateHash,
            RunId = manifest.RunId,
            Transitions = modern
        });
        var requestPath = Path.Combine(root, "seed-progress-request.json");
        Write(requestPath, new MoneyRevenueCropSeedAcquisitionProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = manifest.CommitmentId,
            ExecutionManifestPath = manifestPath
        });
        var proof = MoneyRevenueCropSeedAcquisitionProofBuilder.Build(requestPath);
        Require(proof.SeedAcquisitionVerified &&
                proof.VerifiedQuantityIncrease == 60 &&
                proof.SupportingTransitionCount == 1 &&
                proof.PurchaseSliceCount == 2 &&
                !proof.FormalProductTrainingAuthorized,
            "Applied progress did not preserve exact seed acquisition authority.");

        var cases = new (string Name, Action<JsonObject> Mutate)[]
        {
            ("missing-progress", raw => raw.Remove("queue_progress_verified")),
            ("false-progress", raw => raw["queue_progress_verified"] = false),
            ("stale", raw => raw["after_snapshot_fresh"] = false),
            ("success", raw => raw["success"] = true),
            ("completion", raw => raw["selected_candidate_completed"] = true),
            ("decision", raw => raw["selected_queue_decision_complete"] = true),
            ("retry", raw => raw["selected_queue_continuation_retry_next_iteration"] = false),
            ("reason", raw => raw["queue_replan_stop_reason"] = "unknown"),
            ("resume", raw => raw["selected_queue_resume_index"] = -1),
            ("objective", raw => raw["objective_continuation_completed"] = false),
            ("identity", raw => raw["objective_continuation"]!["shop_id"] = "OtherShop"),
            ("completed-count", raw => raw["completed_objective_continuations"] = new JsonArray()),
            ("run", raw => raw["run_id"] = "other-run"),
            ("hash", raw => raw["source_state_hash"] = "other-state"),
            ("count", raw => raw["executed_item_count"] = 1),
            ("step", raw => raw["step_results"]![0]!["primitive_verification_status"] = "blocked"),
            ("step-completion", raw => raw["step_results"]![0]!["selected_queue_candidate_completed"] = true),
            ("facts", raw => raw["step_results"]![0]!["changed_facts"] = new JsonArray())
        };
        foreach (var test in cases)
        {
            var raw = JsonNode.Parse(File.ReadAllText(
                modern[1].ExecutionReceiptPath))!.AsObject();
            test.Mutate(raw);
            var path = Path.Combine(root, $"seed-progress-{test.Name}-receipt.json");
            Write(path, raw);
            Require(ValidateSeedProgressFixture(
                    modern[1] with { ExecutionReceiptPath = path },
                    allowProgress: true).Length > 0,
                "Applied seed progress admitted forged " + test.Name + ".");
        }
        Console.WriteLine(
            "PASS: 25 applied seed-progress checks; default completion gates and exact acquisition preserved.");
    }

    private static string[] ValidateSeedProgressFixture(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        bool allowProgress)
    {
        T Read<T>(string path) => JsonSerializer.Deserialize<T>(
            File.ReadAllText(path), JsonDefaults.Options)!;
        var before = Read<SnapshotEnvelope>(transition.BeforeSnapshotPath);
        return QueueExecutionReceiptValidator.ValidateContinuationSlice(
            Read<ActionQueueEnvelope>(transition.QueuePath),
            before,
            Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath),
            Read<SnapshotEnvelope>(transition.AfterSnapshotPath),
            transition.ExecutionReceiptPath,
            transition.RunId,
            transition.ExecutorVersion,
            transition.SelectedCandidateId,
            before.StateHash,
            out _,
            allowAppliedVerifiedProgress: allowProgress);
    }
}
