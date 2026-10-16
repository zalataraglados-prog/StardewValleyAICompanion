using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCropProductionObligationPlan()
    {
        var root = TempArtifactRoot("money-crop-production-obligation-plan");
        var snapshot = MoneyCropProductionSnapshot();
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.year2.spring.strawberry",
                SourceDecisionId = "teacher.money.crop.year2.spring.v1",
                SeedId = "745",
                TileCount = 100,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-16T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money crop-production fixture commitment was rejected.");
        var ledger = mutation.Ledger!;

        var snapshotPath = Path.Combine(root, "snapshot.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var requestPath = Path.Combine(root, "request.json");
        Write(snapshotPath, snapshot);
        Write(ledgerPath, ledger);
        Write(requestPath, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = snapshotPath,
            StrategyLedgerPath = ledgerPath
        });

        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            requestPath);
        var commitment = plan.Commitments.Single();
        Require(plan.Status == "crop_production_evidence_collection_ready" &&
                plan.ActiveCommitmentCount == 1 &&
                plan.MinimumGrossRevenue == 60_000 &&
                !plan.MinimumGrossRevenueReachesTarget &&
                !plan.FutureCropProductionProofReady &&
                !plan.FreshSaveRevenueScheduleReady &&
                !plan.FormalProductTrainingAuthorized &&
                commitment.CurrentUnreservedSeedQuantity == 40 &&
                commitment.SeedQuantityShortfall == 60 &&
                commitment.RequiredWateringTotalDays.Length == 24 &&
                commitment.HarvestWaves.Select(value =>
                    value.HarvestTotalDay).SequenceEqual(
                        new[] { 120, 124, 128, 132, 136 }) &&
                commitment.MinimumTotalUnits == 500 &&
                commitment.PendingEvidenceRequestIds.Length == 9 &&
                commitment.PendingEvidenceRequestIds.Contains(
                    "seed-acquisition:money.year2.spring.strawberry:60",
                    StringComparer.Ordinal),
            "Money crop-production obligation plan lost an exact future obligation.");

        ledger.CropPlantingCommitments[0].HarvestTotalDays =
            new[] { 120 };
        var tamperedLedgerPath = Path.Combine(root, "ledger-tampered.json");
        var tamperedRequestPath = Path.Combine(root, "request-tampered.json");
        Write(tamperedLedgerPath, ledger);
        Write(tamperedRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = snapshotPath,
                StrategyLedgerPath = tamperedLedgerPath
            });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Money crop-production harvest schedule was admitted.");

        ledger.CropPlantingCommitments[0].HarvestTotalDays = null!;
        Write(tamperedLedgerPath, ledger);
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A null Money crop-production harvest schedule was admitted.");
    }

    private static SnapshotEnvelope MoneyCropProductionSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "identity": {
                "save_id": {"value":"MoneyCropFarm","status":"available"},
                "player_id": {"value":"123","status":"available"}
              },
              "time": {
                "total_days": {"value":103,"status":"available"},
                "year": {"value":1,"status":"available"},
                "season": {"value":"winter","status":"available"},
                "day": {"value":20,"status":"available"}
              },
              "farm": {
                "crop_catalog": {"value":[{
                  "seed_id":"745",
                  "seasons":["spring"],
                  "grow_days":8,
                  "regrow_days":4,
                  "harvest_item_id":"400",
                  "harvest_item_qualified_id":"(O)400",
                  "harvest_context_tags":["category_fruits","item_strawberry"],
                  "harvest_unit_sale_price":120,
                  "harvest_min_stack":1,
                  "needs_watering":true
                }],"status":"available"},
                "material_inventory_graph": {"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "ownership_class":"actor_owned",
                    "actor_use_authorized":true,
                    "slots":[{
                      "slot_index":0,
                      "item_id":"745",
                      "qualified_item_id":"(O)745",
                      "context_tags":[],
                      "stack":40
                    }]
                  }],
                  "access_points":[],
                  "workbench_links":[],
                  "quantity_rows":[],
                  "physical_inventory_count":1,
                  "access_point_count":0,
                  "deduplicated_access_point_count":0,
                  "default_shared_resource_policy":"deny_without_explicit_authorization"
                },"status":"available"}
              }
            }
            """,
            JsonDefaults.Options)!;
        var snapshot = new SnapshotEnvelope
        {
            BridgeVersion = "self-test",
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "MoneyCropFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = 100,
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
