using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task DailyDecisionBridgesClockResetOnlyThroughItsOriginalAcceptedSavedReadback()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var original = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var selection = CreateDailyMetadataSelection(owner.CurrentSnapshotPath, f.Witness.Root);
        selection = selection with { DecisionReference = await SaveDaily(owner, selection) };
        Assert.False(DailyReadbackClock(owner, selection, original, original));
        var operation = await f.Admit(owner); await f.Run(owner, operation);
        var loaded = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        Assert.True(original.GameTick > loaded.GameTick);
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        Assert.True(DailyReadbackClock(owner, selection, original, loaded));
        Assert.NotNull(await ReadDaily(owner));
        var root = Path.GetDirectoryName(selection.DecisionReference!.Path)!;
        var unmarked = selection with { DecisionReference = null, ModelPlanPath = selection.DecisionPlanPath,
            QueuePath = selection.DecisionPlanPath, EvidencePath = selection.DecisionRankingPath };
        var oldGuard = await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSharedSourceYieldAsync(unmarked,
            Path.Combine(root, "unmarked-receipt.json"), Path.Combine(root, "unmarked-after.json"), Path.Combine(root, "unmarked-runtime")));
        Assert.Contains("original decision source", oldGuard.Message);
        // The marked source passes ONLY the clock bridge, not the missing fresh native preparation.
        var freshGuard = await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSharedSourceYieldAsync(
            unmarked with { DecisionReference = selection.DecisionReference }, Path.Combine(root, "marked-receipt.json"),
            Path.Combine(root, "marked-after.json"), Path.Combine(root, "marked-runtime")));
        Assert.Contains("selected-candidate refresh", freshGuard.Message);
        Assert.Null(owner.Pending);
        var earlier = NativeVariantTerminalFixture.Copy(loaded); earlier.GameTick--;
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            DailyReadbackClock(owner, selection, original, earlier)).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            DailyReadbackClock(owner, selection with { DecisionPlanPath = selection.DecisionRankingPath }, original, loaded)).InnerException);
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
    }

    [Fact]
    public async Task DailyDecisionExpiresAtTheOriginalAcceptedNextDayWithoutReplayingOrRerankingIt()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true);
        await using var owner = await f.Night.Open();
        var selection = CreateDailyMetadataSelection(owner.CurrentSnapshotPath, f.Witness.Root);
        await SaveDaily(owner, selection);
        Assert.NotNull(await ReadDaily(owner));
        await f.CompleteNight(owner);
        Assert.Null(await ReadDaily(owner));
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Null(owner.Pending);
    }

    private static bool DailyReadbackClock(CropTeacherManagedSeedOwner owner, CropTeacherSharedSourceSelection selection,
        SnapshotEnvelope original, SnapshotEnvelope current) => (bool)typeof(CropTeacherManagedSeedOwner)
        .GetMethod("ValidateRetainedDailySelection", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(owner, [selection, original, current, "declared-daily-metadata-choice"])!;

    private static CropTeacherSharedSourceSelection CreateDailyMetadataSelection(string sourcePath, string root)
    {
        var source = LifecycleRead<SnapshotEnvelope>(sourcePath);
        // Declared metadata-only availability; it is not a physical compilation or native permission.
        var availability = new OptionAvailabilityEnvelope { StateHash = source.StateHash, Options =
            [new OptionAvailability { OptionId = "executor.till_soil", Available = true, ReadEligible = true, EventCandidates =
                [new EventCandidate { CandidateId = "declared-daily-metadata-choice", Kind = "till_soil_tile", Available = true,
                    LocationId = "Farm", TileX = 0, TileY = 0, EstimatedTicks = 60, EnergyCost = 2 }] }] };
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "daily.closed_loop");
        var plan = new DailyPlanCompiler().Compile(rows, source.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
        Assert.NotEmpty(plan.Steps);
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
        var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
        var planPath = Path.Combine(root, "declared-daily-plan.json"); var rankingPath = Path.Combine(root, "declared-daily-ranking.json");
        Fixture.Save(planPath, plan); Fixture.Save(rankingPath, ranking);
        return new(sourcePath, planPath, rankingPath, "", "", "", selected.QueueIndex, []);
    }
}
