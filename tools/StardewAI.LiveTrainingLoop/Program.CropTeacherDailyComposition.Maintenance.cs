using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static CropTeacherDailyCompositionBinding BindCropTeacherDailyOwnedMaintenance(
        MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, DailyPlanCompileRequest policyRequest,
        IReadOnlySet<string> prepared, CropTeacherManagedPlanningProgress progress)
    {
        // Mechanical obligations are absent from the autonomous policy pool by design.
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "farm.maintain_crops" }, commitmentLedger: ledger);
        var maintenance = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, policyRequest.GoalId);
        var policy = policyRequest.RankedEventCandidates;
        var added = maintenance.Where(candidate => !policy.Any(row => row.Kind == candidate.Kind &&
            row.LocationId == candidate.LocationId && row.TileX == candidate.TileX && row.TileY == candidate.TileY)).ToArray();
        var binding = CropTeacherRunPlanner.BindDailyComposition(plan, request, snapshot, ledger, new DailyPlanCompileRequest
        {
            StateHash = policyRequest.StateHash, GoalId = policyRequest.GoalId, ExecutionMode = policyRequest.ExecutionMode,
            MaxCandidates = policyRequest.MaxCandidates, CompileActionQueue = policyRequest.CompileActionQueue,
            UseJointCalendar = policyRequest.UseJointCalendar, RequiredCandidateIds = policyRequest.RequiredCandidateIds,
            RankedEventCandidates = policy.Concat(added).ToArray()
        }, prepared, progress.CompletedHarvestTileKeys, progress.CompletedDayBoundaryKeys);
        if (binding.CompileRequest is { } composed)
        {
            // Only work already owned by the immutable crop plan may enter through this materialization.
            composed.RankedEventCandidates = policy.Concat(added.Where(row =>
                binding.OwnedCandidateIds.Contains(row.CandidateId, StringComparer.Ordinal))).ToArray();
        }
        return binding;
    }

    private static void RecordCropTeacherDailyOwnedMaintenance(JsonObject ranking, CropTeacherDailyCompositionBinding binding)
    {
        if (binding.CompileRequest is not { } composed) return;
        var policyIds = (ranking["ranked_event_candidates"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Select(row => ReadString(row, "candidate_id")).ToHashSet(StringComparer.Ordinal);
        ranking["policy_ranked_candidate_ids"] = JsonSerializer.SerializeToNode(policyIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(), JsonOptions);
        ranking["joint_daily_owned_materialization"] = new JsonObject
        {
            ["source_state_hash"] = binding.SourceStateHash, ["ledger_id"] = binding.LedgerId,
            ["ledger_revision"] = binding.LedgerRevision, ["policy_model_selected"] = false,
            ["candidate_ids"] = JsonSerializer.SerializeToNode(composed.RankedEventCandidates
                .Where(row => !policyIds.Contains(row.CandidateId)).Select(row => row.CandidateId).ToArray(), JsonOptions)
        };
        ranking["ranked_event_candidates"] = JsonSerializer.SerializeToNode(composed.RankedEventCandidates, JsonOptions);
    }
}
