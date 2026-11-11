using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// Pins the original compilation only. Native repair/pickup evidence is a separate boundary.
public sealed class CropTeacherRefillApproachCompilation
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly StrategyCommitmentLedger ledger;

    private CropTeacherRefillApproachCompilation(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string modelPlanPath, string queuePath)
    {
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, modelPlanPath, queuePath }.Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var source = CropTeacherSupportingSource.Read(runRequestPath, sourcePath, ledgerPath, commitmentIds, stepId);
        TotalDay = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days");
        var crop = source.Ledger.CropPlantingCommitments.SingleOrDefault(row => row.CommitmentId == commitmentId &&
            row.Status == StrategyCommitmentStatuses.Active && commitmentIds.Contains(commitmentId, StringComparer.Ordinal));
        Require(crop is not null && crop.RequiredWateringTotalDays.Contains(TotalDay),
            "Crop refill approach is outside its original active watering obligation/day.");
        var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        Require(source.CropLocation == "Farm" && layout.WateringRefillApproachTile is not null,
            "Crop refill approach lacks its original Farm layout target.");
        Target = layout.WateringRefillApproachTile!;
        var can = NativeExecutionSourceSupport.Value(source.Snapshot, "player", "watering_can_state");
        Require(can.ValueKind == JsonValueKind.Object && can.TryGetProperty("water_left", out var water) &&
                water.ValueKind == JsonValueKind.Number && water.TryGetInt32(out _),
            "Crop refill approach water quantity is unavailable or mistyped.");
        var x = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "player", "tile_x");
        var y = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "player", "tile_y");
        var location = NativeExecutionSourceSupport.Value(source.Snapshot, "player", "location_id");
        Require(can.GetProperty("water_left").GetInt32() == 0 && (x != Target.TileX || y != Target.TileY) &&
                location.ValueKind == JsonValueKind.String && location.GetString() == "Farm",
            "Crop refill approach does not match the original empty-can approach branch.");
        var plan = CurrentTeacherFrontierSupport.Read<SmallModelPlanEnvelope>(modelPlanPath, "original crop refill approach plan");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(queuePath, "original crop refill approach queue");
        Require(plan.SchemaVersion == "small_model_plan.v1" && plan.SourceModel == "authoritative_crop_watering_refill_approach.v1" &&
                plan.StateHash == source.Snapshot.StateHash && plan.GoalId == "daily.closed_loop" && plan.PlanType == "mechanical_plan" &&
                plan.Steps is [var move] && move.Kind == "move_to_tile" && move.TargetLocation == "Farm" &&
                move.TargetTileX == Target.TileX && move.TargetTileY == Target.TileY && move.EstimatedMinutes == 12,
            "Crop refill approach changed its original model scope/target/budget.");
        var step = plan.Steps.Single();
        var candidates = step.Preconditions.Where(row => row.StartsWith("candidate_id:", StringComparison.Ordinal))
            .Select(row => row["candidate_id:".Length..]).ToArray();
        Require(candidates is [var candidate] && candidate.StartsWith("crop-watering-refill-approach:", StringComparison.Ordinal) &&
                plan.PlanId == "plan:" + candidate && step.StepId == candidate + ":move" && step.Direction is null && step.WaitTicks is null &&
                step.Preconditions.SequenceEqual(new[] { "candidate_id:" + candidate, "world_ready" }, StringComparer.Ordinal) &&
                step.ExpectedEffects.SequenceEqual(new[] { $"player.tile={Target.TileX},{Target.TileY}", "fresh_snapshot_replan_required=true" }, StringComparer.Ordinal) &&
                step.SafetyConstraints.SequenceEqual(new[] { "transparent_current_map_route_only", "bounded_route_repair_clears=4",
                    "water_crop_retries_only_after_native_refill_approach" }, StringComparer.Ordinal) &&
                step.FailurePolicy.SequenceEqual(new[] { "refresh_snapshot_and_replan" }, StringComparer.Ordinal) &&
                step.Parameters.Length == 3 && step.Parameters.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == 3 &&
                step.Parameters.SingleOrDefault(row => row.Name == "max_route_repair_clears")?.Value == "4" &&
                step.Parameters.SingleOrDefault(row => row.Name == "max_route_repair_minutes")?.Value == "12" &&
                step.Parameters.SingleOrDefault(row => row.Name == "fresh_snapshot_replan_required")?.Value == "true",
            "Crop refill approach changed its original candidate or bounded repair declaration.");
        CropTeacherOriginalQueueCompilation.Verify(queue, new ActionQueueCompiler().Compile(plan, source.Snapshot, source.Ledger),
            "Crop refill approach");
        Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 &&
                queue.Items.All(item => item.OptionId is "executor.move_to_tile" or "executor.clear_obstacle"),
            "Crop refill approach is not the original pending move/repair queue.");
        SourcePath = Path.GetFullPath(sourcePath); QueuePath = Path.GetFullPath(queuePath);
        CandidateId = candidates.Single(); QueueId = queue.QueueId;
        ClearanceQueueItemIds = Array.AsReadOnly(queue.Items.Where(item => item.OptionId == "executor.clear_obstacle")
            .Select(item => item.QueueItemId).ToArray());
        SelectedResourceClearanceSupported = queue.Items.Where(item => item.OptionId == "executor.clear_obstacle").All(item =>
            item.NormalizedCommand.Parameters.Where(parameter => parameter.Name == "clear_completion_mode").ToArray()
                is [{ Value: "wild_tree_removed" }]);
        ledger = source.Ledger;
        inputs.Validate();
    }

    public static CropTeacherRefillApproachCompilation Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, string commitmentId, string stepId, string modelPlanPath, string queuePath) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, commitmentId, stepId, modelPlanPath, queuePath);

    public string SourcePath { get; }
    public string QueuePath { get; }
    public int TotalDay { get; }
    public string CandidateId { get; }
    public string QueueId { get; }
    public MoneyRevenueCropPlantingTile Target { get; }
    public IReadOnlyList<string> ClearanceQueueItemIds { get; }
    internal bool SelectedResourceClearanceSupported { get; }
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(ledger);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public void ValidateInputsUnchanged() => inputs.Validate();
}
