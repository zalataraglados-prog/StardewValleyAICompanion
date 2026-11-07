using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    private const string Id = "crop:chest-seeds";
    private const string Owner = "teacher.crop.owner";
    private const string Step = "support.crop.transfer.1";
    private const string Timestamp = "2026-11-07T06:00:00Z";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed partial class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "crop-seed-transfer-" + Guid.NewGuid().ToString("N"));
        internal SnapshotEnvelope Before { get; }
        internal StrategyCommitmentLedger Ledger { get; private set; }
        internal CropPlantingCommitmentUpsertRequest CropRequest { get; }
        internal ReservationPortfolioCommitRequest ResourceRequest { get; }
        internal string SourcePath => Path.Combine(root, "source.json");
        internal string LedgerPath => Path.Combine(root, "ledger.json");
        internal string RequestPath => Path.Combine(root, "request.json");
        internal string RepositoryPath => Path.Combine(root, "repository");
        internal string RuntimePath => Path.Combine(root, "runtime");
        internal string RunPath => Path.Combine(root, "run");

        internal Fixture(bool multipleDestinations = true, Action<MaterialInventoryGraph>? change = null,
            bool nativeEvidence = false, bool depleteSource = false, bool emptyChest = false, bool noPlayerSeed = false,
            Action<SnapshotEnvelope>? beforeChange = null)
        {
            Directory.CreateDirectory(root);
            Before = MoneyCropTeacherFixture.CreateSnapshot("SeedAcquisitionRuntimeBoundary",
                MoneyCropTeacherFixture.CreateSnapshot("MoneyCropProductionSnapshot"));
            CropTeacherNativeSupportFixture.CompleteResources(Before, 500);
            var clock = JsonNode.Parse(Before.State["time"].GetRawText())!.AsObject();
            clock["time"] = NativeField(600, Before.GameTick);
            Before.State["time"] = JsonSerializer.SerializeToElement(clock, JsonOptions);
            var native = StardewAI.Core.Tests.MaterialTransferTestFixture.Snapshot(false);
            foreach (var domain in new[] { "current_location", "locations", "menus" })
            {
                var fields = JsonNode.Parse(native.State[domain].GetRawText())!.AsObject();
                foreach (var field in fields)
                    if (field.Value is JsonObject envelope && envelope.ContainsKey("read_at_tick"))
                        envelope["read_at_tick"] = Before.GameTick;
                Before.State[domain] = JsonSerializer.SerializeToElement(fields, JsonOptions);
            }
            Assert.True(MaterialTransferIntentBinder.TryReadGraph(native, out var graph));
            graph!.PlayerId = 123;
            var player = graph.InventoryNodes[0];
            player.NodeId = "player:123";
            foreach (var node in graph.InventoryNodes) node.OwnerPlayerId = 123;
            player.Slots = noPlayerSeed ? System.Array.Empty<MaterialInventorySlot>() : new[] { Slot(0, "(O)745", multipleDestinations ? 998 : 20) };
            graph.InventoryNodes[1].Slots = new[] { Slot(5, "(O)745", depleteSource || emptyChest ? 10 : 12), Slot(9, "(O)390", 3) }
                .Where(row => !emptyChest || row.QualifiedItemId == "(O)745").ToArray();
            graph.InventoryNodes[2].Slots = new[] { Slot(0, "(O)330", 7) };
            graph.PhysicalInventoryCount = graph.InventoryNodes.Length;
            graph.AccessPointCount = graph.DeduplicatedAccessPointCount = graph.AccessPoints.Length;
            graph.QuantityRows = graph.InventoryNodes.SelectMany(row => row.Slots).GroupBy(row => (row.QualifiedItemId, row.Quality))
                .Select(rows => new MaterialQuantityRow { QualifiedItemId = rows.Key.QualifiedItemId, Quality = rows.Key.Quality,
                    AvailableQuantity = rows.Sum(row => row.Stack), SourceSlotCount = rows.Count() }).ToArray();
            change?.Invoke(graph);
            var farm = JsonNode.Parse(Before.State["farm"].GetRawText())!;
            farm["material_inventory_graph"] = JsonNode.Parse(native.State["farm"].GetProperty("material_inventory_graph").GetRawText());
            farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph, JsonOptions);
            farm["material_inventory_graph"]!["read_at_tick"] = Before.GameTick;
            var actor = JsonNode.Parse(Before.State["player"].GetRawText())!;
            actor["energy"] = NativeField(100d, Before.GameTick);
            actor["location_id"] = JsonNode.Parse(native.State["player"].GetProperty("location_id").GetRawText());
            actor["tile_x"] = JsonNode.Parse(native.State["player"].GetProperty("tile_x").GetRawText());
            actor["tile_y"] = JsonNode.Parse(native.State["player"].GetProperty("tile_y").GetRawText());
            foreach (var name in new[] { "location_id", "tile_x", "tile_y" }) actor[name]!["read_at_tick"] = Before.GameTick;
            actor["inventory"]!["value"] = JsonSerializer.SerializeToNode(player.Slots.Select(row => new
            {
                slot_index = row.SlotIndex, qualified_item_id = row.QualifiedItemId, stack = row.Stack,
                quality = row.Quality, is_empty = false
            }), JsonOptions);
            if (nativeEvidence) CompleteNativeEchoes(farm.AsObject(), actor.AsObject(), graph);
            Before.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            Before.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
            beforeChange?.Invoke(Before);
            Before.StateHash = SnapshotHash.ComputeStateHash(Before.State);
            CropRequest = new CropPlantingCommitmentUpsertRequest
            {
                StateHash = Before.StateHash, CommitmentId = Id, SourceDecisionId = Owner, SeedId = "745",
                TileCount = 10, PlantingYear = 2, PlantingSeason = "spring", PlantingDayOfMonth = 1
            };
            var crop = new CropCommitmentLedgerService().Upsert(null, Before, CropRequest, Timestamp);
            Assert.True(crop.Accepted, string.Join(",", crop.Errors));
            ResourceRequest = new ReservationPortfolioCommitRequest
            {
                StateHash = Before.StateHash, ExpectedLedgerRevision = crop.Ledger!.Revision,
                PortfolioId = "fixture.seeds", SourceDecisionId = Owner, GoalId = "earn_money",
                MaterialClaims = new[]
                {
                    Claim("owned-seeds", Owner, "chest:Farm:4,5", 5, "(O)745", 10, "crop_seed_supply:" + Id),
                    Claim("other-seeds", "other-root", "chest:Farm:4,5", 5, "(O)745", 2, "other-supply"),
                    Claim("other-stone", "other-root", "chest:Farm:4,5", 9, "(O)390", 1, "other-supply"),
                    Claim("outside", "other-root", "chest:Farm:8,5", 0, "(O)330", 1, "other-supply")
                }.Where(row => !(depleteSource || emptyChest) || row.ReservationId != "other-seeds")
                    .Where(row => !emptyChest || row.ReservationId != "other-stone").ToArray()
            };
            var resources = new ReservationPortfolioLedgerService().Commit(crop.Ledger, Before, ResourceRequest, Timestamp);
            Assert.True(resources.Accepted, string.Join(",", resources.Errors));
            Ledger = resources.Ledger!;
            Save(SourcePath, Before);
            Save(LedgerPath, Ledger);
            Save(RequestPath, new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                LayoutIntents = new[] { new MoneyRevenueCropNativeProductionLayoutIntent(Id, "Farm",
                    Enumerable.Range(0, 10).Select(x => new MoneyRevenueCropPlantingTile(x, 0)).ToArray()) }
            });

            MaterialReservationUpsertRequest Claim(string id, string owner, string node, int slot, string item, int count, string purpose) => new()
            {
                StateHash = Before.StateHash, ExpectedLedgerRevision = crop.Ledger!.Revision, ReservationId = id,
                SourceDecisionId = owner, GoalId = "earn_money", NodeId = node, SlotIndex = slot,
                QualifiedItemId = item, Quantity = count, Purpose = purpose
            };
        }

        internal CropTeacherSeedTransferSupport Open(string reservationId = "owned-seeds") =>
            CropTeacherSeedTransferSupport.Open(RequestPath, SourcePath, LedgerPath, Id, Step, reservationId);

        internal ReservationPortfolioCommitResult Prepare(CropTeacherSeedTransferSupport support) =>
            new ReservationPortfolioLedgerService().Commit(Ledger, Before, support.PreparationRequest, Timestamp);

        internal void AdoptLedger(StrategyCommitmentLedger ledger)
        {
            Ledger = ledger;
            Save(LedgerPath, Ledger);
        }

        internal ActionQueueEnvelope Compile(CropTeacherSeedTransferMarker marker) => new ActionQueueCompiler().Compile(new SmallModelActionEnvelope
        {
            ModelOutputId = "fixture.crop.seed.transfer", SourceModel = "deterministic.fixture", StateHash = Before.StateHash,
            GoalId = "earn_money", Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
            Actions = new[] { marker.BuildAction(4, 6) }
        }, Before, marker.Ledger);

        internal static void Save<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

        private static MaterialInventorySlot Slot(int index, string item, int count) => new()
        {
            SlotIndex = index, QualifiedItemId = item, Stack = count, Quality = 0,
            MaximumStackSize = 999, RuntimeType = "StardewValley.Object"
        };

        public void Dispose()
        {
            var control = Path.Combine(RunPath, "live-snapshots", "crop-teacher-seed-transfer-0001");
            foreach (var directory in new[] { Path.Combine(control, "teacher-ledgers"), control,
                         Path.Combine(RunPath, "live-snapshots"), RunPath,
                         Path.Combine(RuntimePath, "teacher-ledgers"), RuntimePath })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (var path in Directory.EnumerateFiles(directory)) File.Delete(path);
                Directory.Delete(directory);
            }
            if (Directory.Exists(RepositoryPath))
            {
                foreach (var path in Directory.EnumerateFiles(RepositoryPath)) File.Delete(path);
                Directory.Delete(RepositoryPath);
            }
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
