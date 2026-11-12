using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed partial class OvernightFixture
    {
        internal async Task Farm(CropTeacherManagedSeedOwner owner)
        {
            var source = owner.CurrentSnapshotPath;
            var day = Read(source).State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
            var step = new CropTeacherRunStep(CropTeacherRunStageKinds.RouteToFarm, day, "", "Farm", "FarmHouse", "", "", 0, 0, null, "declared_return_to_crops");
            var route = await Shared.Prepare(owner, 4, step, Shared.Files.Native + "-farm-return");
            await owner.ExecuteOriginalRouteAsync(route, Shared.Runtime.Client, () => Shared.EmitNative(4, source, snapshot =>
                ChangeState(snapshot, "current_location", node =>
                {
                    node["home_context"]!["value"]!["current_location_is_home"] = false;
                    node["crops"] = JsonNode.Parse(snapshot.State["farm"].GetRawText())!["crops"]!.DeepClone();
                })));
            await Shared.Rebind(owner, route);
        }

        internal Task<CropTeacherManagedSeedOperation> PrepareHarvest(CropTeacherManagedSeedOwner owner)
        {
            var day = Read(owner.CurrentSnapshotPath).State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
            var batch = Shared.Inputs.Plan.Days.Single(row => row.TotalDay == day).ActionBatches.Single(row => row.OptionId == "executor.harvest_crop");
            var step = new CropTeacherRunStep(CropTeacherRunStageKinds.ExecuteCropBatch, day, Id, "Farm", "FarmHouse", "", "", 0, 0, batch, "declared_owned_harvest");
            return InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedHarvestAsync", Shared.Runtime.Client, Shared.Runtime.LiveOptions,
                5, step, Shared.Inputs.Plan, Shared.Inputs.Request, owner, HarvestRuntime);
        }

        internal Task<JsonObject> EmitHarvest(CropTeacherManagedSeedOwner owner)
        {
            NativeHarvestCalls++;
            var before = Read(owner.CurrentSnapshotPath); var current = before;
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(HarvestQueue), JsonOptions)!;
            var snapshots = new List<SnapshotEnvelope>(); var paths = new List<string>();
            foreach (var item in queue.Items)
            {
                var next = Copy(current); next.GameTick++;
                var parameters = item.NormalizedCommand.Parameters;
                var tile = new MoneyRevenueCropPlantingTile(int.Parse(parameters.Single(row => row.Name == "target_tile_x").Value),
                    int.Parse(parameters.Single(row => row.Name == "target_tile_y").Value));
                HarvestFixture.ApplyHarvestDelta(next, tile, Shared.Source.Ledger.CropPlantingCommitments.Single().HarvestItemQualifiedId);
                next.StateHash = SnapshotHash.ComputeStateHash(next.State);
                var path = snapshots.Count == queue.Items.Length - 1 ? HarvestAfter :
                    Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "owned-harvest-step-" + snapshots.Count + ".json");
                snapshots.Add(next); paths.Add(path); Fixture.Save(path, next); current = next;
            }
            var candidate = queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", queue, before, current,
                snapshots.ToArray(), paths.ToArray(), Shared.Files.Options.RunId, candidate);
            var receiptPath = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "execution-0005.json");
            Fixture.Save(receiptPath, receipt); Shared.Runtime.Store.Snapshots[current.StateHash] = current;
            return Task.FromResult(new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = HarvestAfter });
        }

        internal Task RebindHarvest(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
            owner.RunHarvestAsync(operation, Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Shared.Runtime.Client, Shared.Runtime.LiveOptions, diagnostic, request), Shared.Files.Post);
    }
}
