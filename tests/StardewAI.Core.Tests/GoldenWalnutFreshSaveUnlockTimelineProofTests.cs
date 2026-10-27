using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveUnlockTimelineProofTests
{
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void DerivesUnlockDaysFromDeterministicSourceCompletionSchedule()
    {
        var proof = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(
            Schedule(firstIslandArrivalTotalDay: 200),
            Sha256);

        Assert.Equal(
            "golden_walnut_fresh_save_unlock_timeline_proof.v1",
            proof.SchemaVersion);
        Assert.Equal(200, proof.FirstParrotUnlockTotalDay);
        Assert.Equal(201, proof.WesternTurtleUnlockTotalDay);
        Assert.Equal(202, proof.IslandHouseUnlockTotalDay);
        Assert.Equal(200, proof.FirstParrotWalnutReadyTotalDay);
        Assert.Equal(201, proof.WesternTurtleWalnutReadyTotalDay);
        Assert.Equal(202, proof.IslandHouseWalnutReadyTotalDay);
        Assert.True(proof.SourceScheduleComplete);
        Assert.True(proof.NativePhaseOrderingComplete);
        Assert.True(proof.GourmandDeadlineCompositionComplete);
        Assert.False(proof.TrainingAdmissionEligible);
        Assert.Empty(proof.BlockingReasons);
        Assert.True(proof.PlannedDeterministicWalnutQuantity >= 31);
    }

    [Fact]
    public void RouteConsumesHashBoundTimelineWithoutPromotingRuntimeEvidence()
    {
        var timeline = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(
            Schedule(firstIslandArrivalTotalDay: 200),
            Sha256);

        var proof = GoldenWalnutResortFundingRouteProofBuilder.Build(timeline);

        Assert.True(proof.RouteSelectionComplete);
        Assert.False(proof.TrainingAdmissionEligible);
        var route = proof.Alternatives.Single(row =>
            row.RouteId == GoldenWalnutResortFundingRouteProofBuilder.PreferredRouteId);
        Assert.Contains(
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.SchemaVersion,
            route.BoundEvidenceIds);
        Assert.Contains("sha256:" + Sha256, route.BoundEvidenceIds);
        Assert.Equal(
            new[] { "second_and_third_gourmand_native_continuation_receipts" },
            route.MissingEvidenceIds);
    }

    [Fact]
    public void KeepsLateDerivedTimelineClosed()
    {
        var proof = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(
            Schedule(firstIslandArrivalTotalDay: 211),
            Sha256);

        Assert.Equal(212, proof.WesternTurtleUnlockTotalDay);
        Assert.Equal(213, proof.IslandHouseUnlockTotalDay);
        Assert.False(proof.GourmandDeadlineCompositionComplete);
        Assert.Contains(
            "gourmand_unlock_timeline_misses_day_224_deadline",
            proof.BlockingReasons);
    }

    [Fact]
    public void PreservesPlannedUnlockActionDayAfterWalletBecomesReady()
    {
        var schedule = Schedule(firstIslandArrivalTotalDay: 200) with
        {
            PlannedIslandHouseUnlockTotalDay = 203
        };

        var proof = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(
            schedule,
            Sha256);

        Assert.Equal(202, proof.IslandHouseWalnutReadyTotalDay);
        Assert.Equal(203, proof.IslandHouseUnlockTotalDay);
        Assert.True(proof.GourmandDeadlineCompositionComplete);
    }

    [Fact]
    public void RejectsUnlockActionBeforeWalletThresholdIsReady()
    {
        var schedule = Schedule(firstIslandArrivalTotalDay: 200) with
        {
            PlannedWesternTurtleUnlockTotalDay = 200
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(schedule, Sha256));

        Assert.Contains("eleven-walnut threshold", error.Message);
    }

    [Fact]
    public void RejectsSourceScheduledBeforeItsUnlockPhase()
    {
        var schedule = Schedule(firstIslandArrivalTotalDay: 200);
        var rows = schedule.SourceCompletions
            .Select(row => Source(row.SourceKey).AccessPhase == "west_after_turtle"
                ? row with { PlannedCompletionTotalDay = 200 }
                : row)
            .ToArray();

        var error = Assert.Throws<InvalidOperationException>(() =>
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.Build(
                schedule with { SourceCompletions = rows },
                Sha256));

        Assert.Contains("before the planned Western Turtle unlock", error.Message);
    }

    private static GoldenWalnutFreshSaveSourceSchedule Schedule(
        int firstIslandArrivalTotalDay)
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources
            .Where(source =>
                GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(
                    source.AcquisitionOptionId) &&
                source.OptionalUnlockWalnutCost == 0 &&
                !source.ResortOnly &&
                source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal))
            .ToArray();
        var rows = sources.Select(source => new GoldenWalnutFreshSaveSourceCompletion(
            source.SourceKey,
            source.NativeQuantity,
            firstIslandArrivalTotalDay + (source.AccessPhase switch
            {
                "boat_repaired" or "island_south_initial" => 0,
                "north_after_first_parrot" or "east_after_first_parrot" or
                    "volcano_after_first_parrot" or "caldera_after_volcano" => 1,
                "west_after_turtle" => 2,
                _ => throw new InvalidOperationException(
                    "Unexpected deterministic pre-House phase: " + source.AccessPhase)
            })))
            .ToArray();
        return new GoldenWalnutFreshSaveSourceSchedule(
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.SourceSchemaVersion,
            "fresh-save-schedule:test",
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GoalId,
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            false,
            Sha256,
            Sha256,
            "fresh-save-run:test",
            "fresh-save:test",
            4,
            firstIslandArrivalTotalDay,
            firstIslandArrivalTotalDay,
            firstIslandArrivalTotalDay + 1,
            firstIslandArrivalTotalDay + 2,
            rows);
    }

    private static GoldenWalnutNativeSourceDefinition Source(string sourceKey) =>
        GoldenWalnutNativeSourceCatalog.Sources.Single(source =>
            source.SourceKey == sourceKey);
}
