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
    private sealed record ComposedCropDispatch(CropTeacherRunStep? Step, bool ControlPreempted);

    private static async Task<ComposedCropDispatch> ComposeCropTeacherDailyDispatchAsync(HttpClient http, LiveTrainingOptions options,
        int iteration, MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        CropTeacherManagedSeedOwner owner, IReadOnlySet<string> preparedCommitmentIds, SnapshotEnvelope snapshot)
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
            return new(null, true);
        var progress = owner.ReadPlanningProgress();
        await IngestCropTeacherManagedSnapshotAsync(http, options, owner.CurrentSnapshotPath, artifacts, snapshot).ConfigureAwait(false);
        CropTeacherDailyCompositionBinding? binding = null;
        var built = await BuildQueueFromDailyPlanAsync(http, options, snapshot.StateHash, Array.Empty<JsonObject>(),
            candidateKindOverride: string.Empty, candidateIdOverride: string.Empty,
            prepareCompositionRequest: input =>
            {
                binding = CropTeacherRunPlanner.BindDailyComposition(plan, request, snapshot, ledger, input, preparedCommitmentIds,
                    progress.CompletedHarvestTileKeys, progress.CompletedDayBoundaryKeys);
                return binding.CompileRequest ?? throw new InvalidDataException("Original crop prefix must complete before daily composition: " +
                    string.Join(",", binding.BlockingReasons));
            }).ConfigureAwait(false);
        artifacts.Validate();
        var prefix = Path.Combine(options.SnapshotDir, "crop-joint-daily-" + iteration.ToString("D4"));
        await WritePortfolioJournalFileAsync(prefix + "-plan.json", built.Plan.ToJsonString(JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(prefix + "-ranking.json", built.Ranking.ToJsonString(JsonOptions)).ConfigureAwait(false);
        // Existing exclusive recovery preempts the composition hook; never dispatch its queue as crop work.
        if (binding is null) return new(null, true);
        return new(CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, built.Plan.Deserialize<SmallModelPlanEnvelope>(JsonOptions)!), false);
    }
}
