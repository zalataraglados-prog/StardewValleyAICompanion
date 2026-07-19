using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed class StrategyCommitmentLedgerTests
{
    [Fact]
    public void NativeCropCatalogBindsCrossSeasonCommitmentAndRevision()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new CropCommitmentLedgerService();
        var first = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z");

        Assert.True(first.Accepted, string.Join(";", first.Errors));
        Assert.Equal(1, first.Ledger!.Revision);
        var commitment = Assert.Single(first.Ledger.CropPlantingCommitments);
        Assert.Equal("400", commitment.HarvestItemId);
        Assert.Equal("(O)400", commitment.HarvestItemQualifiedId);
        Assert.Equal(new[] { "category_fruits", "item_strawberry" }, commitment.HarvestContextTags);
        Assert.Equal(112, commitment.PlantingTotalDay);
        Assert.Equal(120, commitment.FirstHarvestTotalDay);
        Assert.Equal(136, commitment.LastInSeasonHarvestTotalDay);
        Assert.Equal(4, commitment.RegrowDays);
        Assert.Equal(100, commitment.MinimumUnitsPerWave);
        Assert.Equal("upsert", Assert.Single(first.Ledger.History).Operation);

        var revised = service.Upsert(first.Ledger, snapshot, Request(snapshot, 1, 120), "2026-07-19T00:01:00Z");
        Assert.True(revised.Accepted, string.Join(";", revised.Errors));
        Assert.Equal(2, revised.Ledger!.Revision);
        Assert.Equal(2, Assert.Single(revised.Ledger.CropPlantingCommitments).Revision);
        Assert.Equal(120, Assert.Single(revised.Ledger.CropPlantingCommitments).TileCount);
        Assert.Equal(new[] { "upsert", "upsert" }, revised.Ledger.History.Select(row => row.Operation));

        var stale = service.Upsert(revised.Ledger, snapshot, Request(snapshot, 1, 140), "2026-07-19T00:02:00Z");
        Assert.False(stale.Accepted);
        Assert.Contains("ledger_revision_conflict", stale.Errors);
    }

    [Fact]
    public void CancellationAndAutomaticCompletionPreserveHistory()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new CropCommitmentLedgerService();
        var created = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z").Ledger!;
        var cancelled = service.Cancel(created, snapshot, "year2-spring-strawberry", new StrategyCommitmentCancelRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = 1,
            Reason = "strategy_reallocated_tiles"
        }, "2026-07-19T00:01:00Z");

        Assert.True(cancelled.Accepted, string.Join(";", cancelled.Errors));
        Assert.Equal(StrategyCommitmentStatuses.Cancelled, Assert.Single(cancelled.Ledger!.CropPlantingCommitments).Status);
        Assert.Equal("strategy_reallocated_tiles", Assert.Single(cancelled.Ledger.CropPlantingCommitments).CancelReason);
        Assert.Equal(new[] { "upsert", "cancel" }, cancelled.Ledger.History.Select(row => row.Operation));

        var later = Snapshot(137, 2, "spring", 26);
        var active = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z").Ledger!;
        var completed = service.ReconcileCompleted(active, later, "2026-07-19T00:03:00Z");
        Assert.Equal(2, completed.Revision);
        Assert.Equal(StrategyCommitmentStatuses.Completed, Assert.Single(completed.CropPlantingCommitments).Status);
        Assert.Equal(new[] { "upsert", "complete" }, completed.History.Select(row => row.Operation));
    }

    [Fact]
    public void InvalidSeasonIsRejectedInsteadOfInventingCropTiming()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var request = Request(snapshot, 0, 100);
        request.PlantingSeason = "summer";
        request.PlantingDayOfMonth = 1;

        var result = new CropCommitmentLedgerService().Upsert(null, snapshot, request, "2026-07-19T00:00:00Z");

        Assert.False(result.Accepted);
        Assert.Contains("crop_not_plantable_in_committed_season", result.Errors);
    }

    private static CropPlantingCommitmentUpsertRequest Request(SnapshotEnvelope snapshot, int revision, int tileCount) => new()
    {
        StateHash = snapshot.StateHash,
        ExpectedLedgerRevision = revision,
        CommitmentId = "year2-spring-strawberry",
        SourceDecisionId = "strategy.crop.year2.spring.v1",
        SeedId = "745",
        TileCount = tileCount,
        PlantingYear = 2,
        PlantingSeason = "spring",
        PlantingDayOfMonth = 1,
        LocationContext = "outdoor_seasonal"
    };

    private static SnapshotEnvelope Snapshot(int totalDays, int year, string season, int day)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "identity": {
            "save_id": {"value":"TestFarm","status":"available"},
            "player_id": {"value":"123","status":"available"}
          },
          "time": {
            "total_days": {"value":{{totalDays}},"status":"available"},
            "year": {"value":{{year}},"status":"available"},
            "season": {"value":"{{season}}","status":"available"},
            "day": {"value":{{day}},"status":"available"}
          },
          "farm": {
            "crop_catalog": {"value":[{
              "seed_id":"745","seasons":["spring"],"grow_days":8,"regrow_days":4,
              "harvest_item_id":"400","harvest_item_qualified_id":"(O)400","harvest_context_tags":["category_fruits","item_strawberry"],"harvest_min_stack":1
            }],"status":"available"}
          }
        }
        """)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?> { Value = "TestFarm", Status = FieldStatus.Available },
            PlayerId = new FieldEnvelope<string?> { Value = "123", Status = FieldStatus.Available },
            GameTick = totalDays,
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
