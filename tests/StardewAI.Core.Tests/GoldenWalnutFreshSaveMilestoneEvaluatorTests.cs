using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveMilestoneEvaluatorTests
{
    [Fact]
    public void AcceptsExactCatalogAfterAllRequiredNativeMilestones()
    {
        var result = GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate(
            Snapshot(collectedQuantity: 1));

        Assert.True(result.Ready);
        Assert.Equal(1, result.TrackedCollectedQuantity);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void FailsClosedUntilSourceIncrementAndEveryUnlockAreObserved()
    {
        var result = GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate(
            Snapshot(
                collectedQuantity: 0,
                islandHouse: false));

        Assert.False(result.Ready);
        Assert.Contains(
            "golden_walnut_source_increment_unobserved",
            result.BlockingReasons);
        Assert.Contains("island_house_incomplete", result.BlockingReasons);
    }

    [Fact]
    public void RejectsCatalogIdentityDriftEvenWhenUnlocksAreComplete()
    {
        var result = GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate(
            Snapshot(
                collectedQuantity: 1,
                nativeSourceKeyCount:
                    GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount - 1));

        Assert.False(result.Ready);
        Assert.Contains(
            "golden_walnut_source_catalog_identity_invalid",
            result.BlockingReasons);
    }

    [Fact]
    public void RejectsMilestoneReachedAtOrAfterYearThreeDeadline()
    {
        var result = GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate(
            Snapshot(
                collectedQuantity: 1,
                totalDay:
                    GoldenWalnutUnlockScheduleProofBuilder
                        .DeadlineTotalDayExclusive));

        Assert.False(result.Ready);
        Assert.Contains(
            "golden_walnut_deadline_total_day_reached",
            result.BlockingReasons);
    }

    private static SnapshotEnvelope Snapshot(
        int collectedQuantity,
        bool islandHouse = true,
        int totalDay = 100,
        int nativeSourceKeyCount =
            GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount)
    {
        var changedSource = GoldenWalnutNativeSourceCatalog.Sources[0];
        var sources = GoldenWalnutNativeSourceCatalog.Sources
            .Select(source => new
            {
                source_key = source.SourceKey,
                native_quantity = source.NativeQuantity,
                collected_quantity = source.SourceKey == changedSource.SourceKey
                    ? collectedQuantity
                    : 0
            })
            .ToArray();
        var collected = sources.Sum(source => source.collected_quantity);
        var remaining = sources.Sum(source =>
            source.native_quantity - source.collected_quantity);
        return new SnapshotEnvelope
        {
            GameVersion =
                GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            SaveId = new FieldEnvelope<string?>
            {
                Value = "fresh-save",
                Status = FieldStatus.Available
            },
            State = new Dictionary<string, JsonElement>
            {
                ["time"] = JsonSerializer.SerializeToElement(new
                {
                    total_days = new
                    {
                        status = "available",
                        value = totalDay
                    }
                }),
                ["locations"] = JsonSerializer.SerializeToElement(new
                {
                    ginger_island_access = new
                    {
                        status = "available",
                        value = new
                        {
                            schema_version = "ginger_island_access.v1",
                            projection_status =
                                "complete_locked_base_1.6.15_native_contract",
                            journey = new { island_reached = true },
                            first_parrot = new { complete_or_pending = true },
                            western_turtle_parrot = new
                            {
                                complete_or_pending = true
                            },
                            island_house_parrot = new
                            {
                                complete_or_pending = islandHouse
                            }
                        }
                    }
                }),
                ["world_progress"] = JsonSerializer.SerializeToElement(new
                {
                    golden_walnut_sources = new
                    {
                        status = "available",
                        value = new
                        {
                            schema_version =
                                GoldenWalnutNativeSourceCatalog.SchemaVersion,
                            projection_status =
                                GoldenWalnutNativeSourceCatalog.ProjectionStatus,
                            native_source_key_count = nativeSourceKeyCount,
                            native_walnut_total =
                                GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
                            tracked_collected_quantity = collected,
                            tracked_remaining_quantity = remaining,
                            sources
                        }
                    }
                })
            }
        };
    }
}
