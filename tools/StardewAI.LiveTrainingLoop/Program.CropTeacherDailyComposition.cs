using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record CropTeacherDailySelection(int TotalDay, string GoalId, SelectedQueueDecisionLease Lease,
        IReadOnlySet<string> OwnedCandidateIds, string PlanPath, string RankingPath)
    {
        public string SourceSnapshotPath { get; init; } = string.Empty;
        public CropTeacherDailyDecisionReference? DecisionReference { get; init; }
        public IReadOnlySet<string> CompletedForeignCandidateIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, JsonObject> ForeignObjectiveContinuations { get; init; } =
            new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        public IReadOnlyCollection<JsonObject> SuppressedForeignContinuations { get; init; } = Array.Empty<JsonObject>();
    }
    private sealed record CropTeacherOtherGoalPreparation(SelectedQueueCandidateLock Candidate,
        string PlanPath, string QueuePath, string EvidencePath, string QueueStatus)
    {
        public CropTeacherSharedSourceSelection? SourceSelection { get; init; }
        public CropTeacherManagedSeedOperation? Operation { get; init; }
    }
    private sealed record ComposedCropDispatch(CropTeacherRunStep? Step, bool ControlPreempted,
        CropTeacherDailySelection? Selection = null, string BlockReason = "crop_teacher_joint_daily_required_batch_not_scheduled",
        CropTeacherOtherGoalPreparation? OtherGoalPreparation = null)
    {
        public bool ReplanWithoutDispatch { get; init; }
    }

    private static async Task<ComposedCropDispatch> ComposeCropTeacherDailyDispatchAsync(HttpClient http, LiveTrainingOptions options,
        int iteration, MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        CropTeacherManagedSeedOwner owner, IReadOnlySet<string> preparedCommitmentIds, SnapshotEnvelope snapshot,
        CropTeacherDailySelection? selection)
    {
        if (!options.UseDailyPlan || !options.UseJointDailyCalendar)
            throw new InvalidOperationException("Managed daily composition requires explicit joint daily-plan mode.");
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        artifacts.Pin(owner.CurrentSnapshotPath);
        var ledger = artifacts.Read<StrategyCommitmentLedger>(owner.CurrentLedgerPath);
        var storyEvent = snapshot.State.TryGetValue("player", out var player) && player.TryGetProperty("story_event", out var story)
            ? JsonNode.Parse(story.GetRawText()) : null;
        var control = new JsonObject { ["state"] = new JsonObject { ["player"] = new JsonObject { ["story_event"] = storyEvent } } };
        // Let the existing owned control branch choose and verify these interrupts first.
        if (HasPriorityStoryEventInterrupt(control) || SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(snapshot) ||
            SnapshotGameClockBudgetReader.CurrentTime(snapshot) is int now && GameClockBudgetPolicy.RecoveryWindowStarted(now))
            return new(null, true, selection);
        var progress = owner.ReadPlanningProgress();
        var next = CropTeacherRunPlanner.Evaluate(plan, request, snapshot, preparedCommitmentIds,
            progress.CompletedHarvestTileKeys, progress.CompletedDayBoundaryKeys, ledger);
        if (OriginalWateringRefillApproachPreemptsComposition(next, request, snapshot)) return new(null, true, selection);
        await IngestCropTeacherManagedSnapshotAsync(http, options, owner.CurrentSnapshotPath, artifacts, snapshot).ConfigureAwait(false);
        if (selection?.TotalDay == next.TotalDay)
        {
            var refreshed = await RefreshCropTeacherDailySelectionAsync(http, options, iteration, plan, request,
                snapshot, ledger, preparedCommitmentIds, progress, selection, owner).ConfigureAwait(false);
            artifacts.Validate();
            if (refreshed is not null) return refreshed;
            if (next.Stage != CropTeacherRunStageKinds.ExecuteCropBatch)
                return new(null, true, selection.SuppressedForeignContinuations.Count > 0 ? selection : null);
        }
        // Root completion consumes only a retained decision; it must not select another optional day plan.
        if (next.Stage == CropTeacherRunStageKinds.Complete) return new(null, true);
        CropTeacherDailyCompositionBinding? binding = null;
        var suppressed = selection?.SuppressedForeignContinuations ?? Array.Empty<JsonObject>();
        var built = await BuildQueueFromDailyPlanAsync(http, options, snapshot.StateHash, suppressed,
            candidateKindOverride: string.Empty, candidateIdOverride: string.Empty,
            prepareCompositionRequest: input =>
            {
                binding = BindCropTeacherDailyOwnedMaintenance(plan, request, snapshot, ledger, input, preparedCommitmentIds, progress);
                return binding.CompileRequest ?? throw new InvalidDataException("Original crop prefix must complete before daily composition: " +
                    string.Join(",", binding.BlockingReasons));
            }).ConfigureAwait(false);
        artifacts.Validate();
        if (binding is not null) RecordCropTeacherDailyOwnedMaintenance(built.Ranking, binding);
        var prefix = Path.Combine(options.SnapshotDir, "crop-joint-daily-" + iteration.ToString("D4"));
        await WritePortfolioJournalFileAsync(prefix + "-plan.json", built.Plan.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(prefix + "-ranking.json", built.Ranking.ToJsonString(JsonOptions)).ConfigureAwait(false);
        // Existing exclusive recovery preempts the composition hook; never dispatch its queue as crop work.
        if (binding is null) return new(null, true, selection);
        var typedPlan = built.Plan.Deserialize<SmallModelPlanEnvelope>(JsonOptions)!;
        var decision = typedPlan.Steps.Length == 0 ? null : new CropTeacherDailySelection(next.TotalDay, typedPlan.GoalId,
            SelectedQueueDecisionLease.CreateFromPlan(typedPlan, built.Ranking),
            binding.OwnedCandidateIds.ToHashSet(StringComparer.Ordinal), prefix + "-plan.json", prefix + "-ranking.json")
            { SourceSnapshotPath = owner.CurrentSnapshotPath, SuppressedForeignContinuations = suppressed };
        if (decision is not null)
            decision = decision with { DecisionReference = await owner.SaveDailyDecisionAsync(iteration,
                decision.SourceSnapshotPath, decision.PlanPath, decision.RankingPath, decision.OwnedCandidateIds).ConfigureAwait(false) };
        var selectedStep = CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, typedPlan, plan);
        if (selectedStep is null && decision is not null &&
            !decision.OwnedCandidateIds.Contains(decision.Lease.Candidates[0].CandidateId))
        {
            var other = await PrepareCropTeacherSelectedOtherGoalAsync(http, options, iteration, snapshot, decision,
                decision.Lease.Candidates[0], owner).ConfigureAwait(false);
            artifacts.Validate();
            return other;
        }
        return new(selectedStep, false, decision);
    }

    private static async Task<CropTeacherDailySelection?> RestoreCropTeacherDailySelectionAsync(
        CropTeacherManagedSeedOwner owner, int totalDay, CropTeacherDailySelection? selection)
    {
        if (selection?.TotalDay == totalDay) return selection;
        var retained = await owner.ReadDailyDecisionAsync().ConfigureAwait(false);
        if (retained is null || retained.TotalDay != totalDay) return null;
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var restored = new CropTeacherDailySelection(retained.TotalDay, retained.GoalId,
            SelectedQueueDecisionLease.CreateFromPlan(artifacts.Read<SmallModelPlanEnvelope>(retained.PlanPath),
                artifacts.Read<JsonObject>(retained.RankingPath)), retained.OwnedCandidateIds.ToHashSet(StringComparer.Ordinal),
            retained.PlanPath, retained.RankingPath)
        {
            SourceSnapshotPath = retained.SourceSnapshotPath, DecisionReference = retained.Reference,
            CompletedForeignCandidateIds = retained.CompletedForeignCandidateIds,
            ForeignObjectiveContinuations = retained.ForeignObjectiveContinuations
        };
        artifacts.Validate();
        return restored;
    }

    private static bool OriginalWateringRefillApproachPreemptsComposition(CropTeacherRunStep next,
        MoneyRevenueCropNativeProductionRunPlanRequest request, SnapshotEnvelope snapshot)
    {
        if (next.ActionBatch?.OptionId != "executor.water_crop") return false;
        var player = snapshot.State["player"];
        var source = new JsonObject { ["state"] = new JsonObject { ["player"] = new JsonObject
        {
            ["watering_can_state"] = JsonNode.Parse(player.GetProperty("watering_can_state").GetRawText()),
            ["tile_x"] = JsonNode.Parse(player.GetProperty("tile_x").GetRawText()),
            ["tile_y"] = JsonNode.Parse(player.GetProperty("tile_y").GetRawText())
        } } };
        return CropTeacherWateringRefillRecoveryPlanner.Build(next, request,
            ReadSnapshotEnvelopeNestedInt(source, "player", "watering_can_state", "water_left"),
            ReadSnapshotEnvelopeInt(source, "player", "tile_x"), ReadSnapshotEnvelopeInt(source, "player", "tile_y")).ApproachTile is not null;
    }
}
