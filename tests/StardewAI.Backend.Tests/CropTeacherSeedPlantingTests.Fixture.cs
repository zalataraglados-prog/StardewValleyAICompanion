using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "owned-seed-planting-" + Guid.NewGuid().ToString("N"));
        private readonly CropTeacherSeedTransferTests.Fixture transfer;
        internal SnapshotEnvelope Before { get; }
        internal StrategyCommitmentLedger Ledger { get; }
        internal string SourcePath => Path.Combine(root, "source.json");
        internal string LedgerPath => Path.Combine(root, "ledger.json");
        internal string RequestPath => Path.Combine(root, "request.json");
        internal string QueuePath => Path.Combine(root, "queue.json");
        private string RepositoryPath => Path.Combine(root, "repository");
        internal FileStrategyCommitmentRepository Repository { get; }
        internal string Id => Ledger.CropPlantingCommitments.Single().CommitmentId;
        internal string OwnedSlotOne => Ledger.MaterialReservations.Single(row => row.NodeId == "player:123" && row.SlotIndex == 1).ReservationId;

        internal Fixture()
        {
            Directory.CreateDirectory(root);
            transfer = new(nativeEvidence: true, beforeChange: snapshot =>
            {
                var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!.AsObject();
                time["year"]!["value"] = 2;
                time["season"]!["value"] = "spring";
                time["day"]!["value"] = 1;
                time["total_days"]!["value"] = 112;
                snapshot.State["time"] = JsonSerializer.SerializeToElement(time, JsonOptions);
                var location = JsonNode.Parse(snapshot.State["current_location"].GetRawText())!.AsObject();
                location["planting_context"] = JsonSerializer.SerializeToNode(new
                {
                    status = "available", confidence = 1d, read_at_tick = snapshot.GameTick,
                    source = new { kind = "game_object", path = "fixture.owned.planting" }, adapter = "test",
                    value = new { hoe_dirt_tiles = Enumerable.Range(0, 10).Select(x => new
                    {
                        tile_x = x, tile_y = 0, has_crop = false,
                        seed_results = new[] { new { seed_id = "745", hard_rule_allows_planting = true } }
                    }).ToArray() }
                }, JsonOptions);
                snapshot.State["current_location"] = JsonSerializer.SerializeToElement(location, JsonOptions);
            });
            Repository = new FileStrategyCommitmentRepository(RepositoryPath);
            Assert.True(Repository.Upsert(transfer.Before, transfer.CropRequest).Accepted);
            Assert.True(Repository.CommitReservationPortfolio(transfer.Before, transfer.ResourceRequest).Accepted);
            transfer.AdoptLedger(Repository.Get(transfer.Before));
            var transferScope = transfer.Open();
            var marker = transferScope.VerifyPreparationReply(Repository.CommitReservationPortfolio(transfer.Before, transferScope.PreparationRequest));
            var transition = transfer.NativeTransition(marker);
            var verified = transfer.VerifyNative(marker, transition);
            var settled = Repository.SettleReservationPortfolioSupportingTransition(transfer.After, verified.SettlementRequest);
            Ledger = verified.VerifySettlementReply(settled);
            Before = transfer.After;
            Save(SourcePath, Before);
            Save(LedgerPath, Ledger);
            File.Copy(transfer.RequestPath, RequestPath);
        }

        internal CropTeacherSeedPlantingSupport Open() => CropTeacherSeedPlantingSupport.Open(RequestPath, SourcePath, LedgerPath,
            Id, "support.owned.planting.1");
        internal CropTeacherSeedPlantingMarker Mark(CropTeacherSeedPlantingSupport? scope = null)
        {
            scope ??= Open();
            return scope.VerifyMarkerReply(Repository.CommitReservationPortfolio(Before, scope.MarkerRequest));
        }
        internal MoneyRevenueCropNativeProductionActionBatch Batch(int count = 8) => new(1, Id, "executor.plant_seed", "source",
            "Farm", "(O)745", Enumerable.Range(0, count).Select(x => new MoneyRevenueCropPlantingTile(x, 0)).ToArray());
        internal ActionQueueEnvelope Compile(CropTeacherSeedPlantingMarker marker, SmallModelAction[] actions, string goal = "earn_money") =>
            new ActionQueueCompiler().Compile(new SmallModelActionEnvelope
            {
                ModelOutputId = "owned-seed-planting-fixture", SourceModel = "deterministic.fixture", StateHash = Before.StateHash,
                GoalId = goal, Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), Actions = actions
            }, Before, marker.Ledger);
        internal static void Save<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

        public void Dispose()
        {
            transfer.Dispose();
            foreach (var path in Directory.EnumerateFiles(RepositoryPath)) File.Delete(path);
            Directory.Delete(RepositoryPath);
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
