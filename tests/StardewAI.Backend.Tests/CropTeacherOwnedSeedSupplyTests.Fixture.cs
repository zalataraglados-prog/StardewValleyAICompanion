using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherOwnedSeedSupplyTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly MoneyCropTeacherFixture teacher = new("474", 3);
        private readonly string root = Path.Combine(Path.GetTempPath(), "owned-crop-seed-" + Guid.NewGuid().ToString("N"));
        public SnapshotEnvelope Source => teacher.SourceSnapshot;
        public StrategyCommitmentLedger Ledger { get; set; }
        public MoneyRevenueCropNativeProductionRunPlan Plan { get; }
        public MoneyRevenueCropNativeProductionRunPlanRequest Request { get; }
        public string Id => Plan.CoveredCommitmentIds.Single();
        public string DecisionId => Ledger.CropPlantingCommitments[0].SourceDecisionId;

        public Fixture()
        {
            Directory.CreateDirectory(root);
            teacher.AddCurrencyEvidence();
            ChangeGraph(graph =>
            {
                var player = graph["inventory_nodes"]![0]!;
                player["inventory_kind"] = "player_inventory";
                player["owner_player_id"] = 123;
                player["location_id"] = "FarmHouse";
                player["capacity"] = 12;
                player["slots"]![0]!["quality"] = 0;
                player["slots"]![0]!["maximum_stack_size"] = 999;
                player["slots"]![0]!["runtime_type"] = "StardewValley.Object";
            }, player => player["inventory"]!["value"]![0]!["quality"] = 0);
            var layout = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(teacher.LayoutPath), JsonOptions)!;
            layout.State["player"] = Source.State["player"];
            var layoutFarm = JsonNode.Parse(layout.State["farm"].GetRawText())!;
            layoutFarm["material_inventory_graph"] = JsonNode.Parse(Source.State["farm"].GetProperty("material_inventory_graph").GetRawText());
            layoutFarm["crops"] = JsonNode.Parse(Source.State["farm"].GetProperty("crops").GetRawText());
            layout.State["farm"] = JsonSerializer.SerializeToElement(layoutFarm, JsonOptions);
            layout.StateHash = SnapshotHash.ComputeStateHash(layout.State);
            File.WriteAllText(teacher.LayoutPath, JsonSerializer.Serialize(layout, JsonOptions));
            var material = new MaterialReservationLedgerService().Upsert(teacher.EmptyLedger(), Source,
                new MaterialReservationUpsertRequest
                {
                    StateHash = Source.StateHash, ExpectedLedgerRevision = 0, ReservationId = "other-seeds",
                    SourceDecisionId = "other-root", GoalId = "complete_full_shipment", NodeId = "player:inventory",
                    SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = 2, Purpose = "other root supply"
                }, "2026-11-07T00:00:00Z");
            Assert.True(material.Accepted, string.Join(",", material.Errors));
            var currency = new CurrencyReservationLedgerService().Upsert(material.Ledger, Source,
                new CurrencyReservationUpsertRequest
                {
                    StateHash = Source.StateHash, ExpectedLedgerRevision = 1, ReservationId = "other-money",
                    SourceDecisionId = "other-root", GoalId = "marriage_and_house_upgrade", CurrencyId = 0,
                    Amount = 350, Purpose = "other root construction"
                }, "2026-11-07T00:00:00Z");
            Assert.True(currency.Accepted, string.Join(",", currency.Errors));
            teacher.SetLedger(currency.Ledger!);
            var decisionPath = teacher.SaveDecision(teacher.Build());
            var receipt = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(decisionPath);
            Ledger = MoneyRevenueCropResourceReservationBuilder.Build(decisionPath).ReservationResult.Ledger!;
            var ledgerPath = Save("planning-ledger.json", receipt.FinalLedger);
            var obligationRequest = Save("obligation-request.json", new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = teacher.SnapshotPath, StrategyLedgerPath = ledgerPath
            });
            var obligation = Save("obligation.json", MoneyRevenueCropProductionObligationPlanBuilder.Build(obligationRequest));
            Request = new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = obligation, LayoutIntents = receipt.LayoutIntents,
                SeedAcquisitionIntents = new[] { new MoneyRevenueCropNativeSeedAcquisitionIntent(
                    receipt.LayoutIntents.Single().CommitmentId, "(O)474", "SeedShop", 80) }
            };
            Plan = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(Save("run-plan-request.json", Request));
        }

        public CropTeacherOwnedSeedSupply Supply() => CropTeacherOwnedSeedSupply.Read(Source, Ledger, new[] { Id });
        public CropTeacherRunStep Evaluate() => CropTeacherRunPlanner.Evaluate(Plan, Request, Source, resourceLedger: Ledger);

        public void ChangeGraph(Action<JsonObject> change, Action<JsonObject>? playerChange = null)
        {
            if (playerChange is not null) teacher.ChangePlayer(playerChange);
            var farm = JsonNode.Parse(Source.State["farm"].GetRawText())!;
            farm["crops"] ??= JsonSerializer.SerializeToNode(new FieldEnvelope<object[]>
            {
                Status = FieldStatus.Available, Value = Array.Empty<object>()
            }, JsonOptions);
            change(farm["material_inventory_graph"]!["value"]!.AsObject());
            Source.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            Source.StateHash = SnapshotHash.ComputeStateHash(Source.State);
            File.WriteAllText(teacher.SnapshotPath, JsonSerializer.Serialize(Source, JsonOptions));
        }

        public void Anchor()
        {
            var result = new ReservationPortfolioLedgerService().Commit(Ledger, Source,
                new ReservationPortfolioCommitRequest
                {
                    StateHash = Source.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                    PortfolioId = "test-source-rebind:" + Source.StateHash,
                    SourceDecisionId = "test-source-rebind", GoalId = "earn_money"
                }, "2026-11-07T00:30:00Z");
            Assert.True(result.Accepted, string.Join(",", result.Errors));
            Ledger = result.Ledger!;
        }

        public void PutOwnedSeedInChest(string location)
        {
            ChangeGraph(graph =>
            {
                graph["inventory_nodes"]![0]!["slots"]![0]!["stack"] = 2;
                graph["inventory_nodes"]!.AsArray().Add(JsonSerializer.SerializeToNode(new MaterialInventoryNode
                {
                    NodeId = "chest:seeds", InventoryKind = "chest", SupplyState = "available", LocationId = location,
                    OwnerPlayerId = 123, ActorUseAuthorized = true, Capacity = 36,
                    Slots = new[] { new MaterialInventorySlot { SlotIndex = 0, QualifiedItemId = "(O)474",
                        Stack = 1, Quality = 0, MaximumStackSize = 999, RuntimeType = "StardewValley.Object" } }
                }, JsonOptions));
                graph["physical_inventory_count"] = 2;
                graph["access_points"] = JsonSerializer.SerializeToNode(new[] { new MaterialInventoryAccessPoint
                {
                    NodeId = "chest:seeds", AccessPointId = "chest-access", LocationId = location,
                    AccessKind = "placed_chest", SpecialChestType = "None", ActorUseAuthorized = true
                } }, JsonOptions);
                graph["access_point_count"] = 1;
            }, player => player["inventory"]!["value"]![0]!["stack"] = 2);
            Ledger.MaterialReservations.Single(row => row.Purpose == "crop_seed_supply:" + Id).NodeId = "chest:seeds";
            Anchor();
        }

        private string Save<T>(string name, T value)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
            return path;
        }

        public void Dispose()
        {
            teacher.Dispose();
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
