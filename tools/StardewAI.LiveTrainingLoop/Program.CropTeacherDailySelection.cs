using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ComposedCropDispatch?> RefreshCropTeacherDailySelectionAsync(HttpClient http, LiveTrainingOptions options,
        int iteration, MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, IReadOnlySet<string> prepared,
        CropTeacherManagedPlanningProgress progress, CropTeacherDailySelection selection)
    {
        // Materialize the owned maintenance surface only. The policy's high-level order remains locked.
        var availabilityNode = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/options/availability",
            JsonSerializer.Serialize(new
            {
                state_hash = snapshot.StateHash,
                candidates = new[] { new { option_id = "farm.maintain_crops", explicit_confirmation_granted = false,
                    invocation_source = options.DailyPlanInvocationSource, parameters = Array.Empty<SmallModelActionParameter>() } }
            }, JsonOptions)).ConfigureAwait(false);
        var availability = availabilityNode.Deserialize<OptionAvailabilityEnvelope>(JsonOptions)
            ?? throw new InvalidDataException("Selected daily maintenance availability is empty.");
        if (availability.StateHash != snapshot.StateHash)
            throw new InvalidDataException("Selected daily maintenance availability belongs to a different snapshot.");
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, selection.GoalId);
        var binding = CropTeacherRunPlanner.BindDailyComposition(plan, request, snapshot, ledger, new DailyPlanCompileRequest
        {
            StateHash = snapshot.StateHash, GoalId = selection.GoalId, ExecutionMode = options.TargetExecutionMode,
            RankedEventCandidates = candidates, MaxCandidates = options.DailyPlanMaxCandidates
        }, prepared, progress.CompletedHarvestTileKeys, progress.CompletedDayBoundaryKeys);
        if (binding.CompileRequest is null) throw new InvalidDataException("Selected daily maintenance lost its original prefix: " +
            string.Join(",", binding.BlockingReasons) + "; native candidates=" + string.Join(";", availability.Options
                .SelectMany(option => option.EventCandidates).Take(8).Select(row => row.CandidateId + ":" + string.Join(",", row.BlockReasons))) +
            "; options=" + string.Join(";", availability.Options.Select(row => row.OptionId + ":missing=" +
                string.Join(",", row.MissingStateFactors) + ":blocked=" + string.Join(",", row.BlockingReasons))));
        var required = candidates.Where(row => binding.RequiredCandidateIds.Contains(row.CandidateId, StringComparer.Ordinal)).ToArray();
        var pending = new List<SelectedQueueCandidateLock>();
        foreach (var selected in selection.Lease.Candidates)
        {
            if (selection.RequiredCandidateIds.Contains(selected.CandidateId))
            {
                var original = selected.RankedCandidate.Deserialize<PolicyEventCandidatePrediction>(JsonOptions)!;
                var sameTarget = required.Where(row => row.Kind == original.Kind && row.LocationId == original.LocationId &&
                    row.TileX == original.TileX && row.TileY == original.TileY).ToArray();
                // Only the original owner's current source/progress can retire its now-satisfied work.
                if (sameTarget.Length == 0) continue;
                if (sameTarget.Length != 1 || sameTarget[0].CandidateId != selected.CandidateId)
                    throw new InvalidDataException("Selected daily maintenance changed its materialized identity.");
            }
            pending.Add(selected);
        }
        if (pending.Count == 0) return null; // A completed high-level decision may now select the next one.
        var prefix = pending.TakeWhile(row => selection.RequiredCandidateIds.Contains(row.CandidateId)).ToArray();
        var evidence = new JsonObject
        {
            ["schema_version"] = "selected_joint_daily_refresh.v1", ["state_hash"] = snapshot.StateHash,
            ["decision_plan_path"] = selection.PlanPath, ["decision_ranking_path"] = selection.RankingPath,
            ["policy_model_invoked"] = false, ["selection_locked"] = true,
            ["pending_selected_candidate_ids"] = JsonSerializer.SerializeToNode(pending.Select(row => row.CandidateId).ToArray(), JsonOptions),
            ["selected_queue_indices"] = JsonSerializer.SerializeToNode(prefix.Select(row => row.QueueIndex).ToArray(), JsonOptions)
        };
        var output = Path.Combine(options.SnapshotDir, "crop-joint-daily-" + iteration.ToString("D4"));
        if (prefix.Length == 0)
        {
            evidence["status"] = "other_owner_required";
            await WritePortfolioJournalFileAsync(output + "-ranking.json", evidence.ToJsonString(JsonOptions)).ConfigureAwait(false);
            return new(null, false, selection, "crop_teacher_joint_daily_selected_other_owner_required");
        }
        var materialized = JsonSerializer.SerializeToNode(candidates, JsonOptions)!.AsArray();
        var locked = prefix.Select((selected, index) =>
        {
            var matches = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(materialized, selected);
            if (matches.Count != 1) throw new InvalidDataException("Selected daily maintenance is unavailable or ambiguous on the fresh source.");
            var row = matches[0]!.Deserialize<PolicyEventCandidatePrediction>(JsonOptions)!;
            row.Rank = index + 1;
            return row;
        }).ToArray();
        var response = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/daily-plan/compile",
            JsonSerializer.Serialize(new DailyPlanCompileRequest
            {
                StateHash = snapshot.StateHash, GoalId = selection.GoalId, ExecutionMode = options.TargetExecutionMode,
                UseJointCalendar = true, CompileActionQueue = false, MaxCandidates = locked.Length,
                RankedEventCandidates = locked, RequiredCandidateIds = locked.Select(row => row.CandidateId).ToArray()
            }, JsonOptions)).ConfigureAwait(false);
        var compiled = response["plan"]?.AsObject() ?? throw new InvalidDataException("Selected daily refresh returned no plan.");
        evidence["status"] = "fresh_compiled";
        evidence["ranked_event_candidates"] = JsonSerializer.SerializeToNode(locked, JsonOptions);
        await WritePortfolioJournalFileAsync(output + "-plan.json", compiled.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(output + "-ranking.json", evidence.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return new(CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, compiled.Deserialize<SmallModelPlanEnvelope>(JsonOptions)!, plan), false, selection);
    }
}
