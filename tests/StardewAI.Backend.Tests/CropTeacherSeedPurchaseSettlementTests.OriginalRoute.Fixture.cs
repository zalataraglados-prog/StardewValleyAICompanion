using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class OwnedRouteFixture : IDisposable
    {
        internal Fixture Source { get; }
        internal RuntimeFixture Runtime { get; }
        internal OwnerInputs Inputs { get; }
        internal OwnerFiles Files { get; }
        internal string QueuePath => Path.Combine(Runtime.LiveOptions.SnapshotDir, "crop-teacher-route-0001-compiled-queue.json");
        internal string ReceiptPath => Path.Combine(Runtime.LiveOptions.SnapshotDir, "execution-0001.json");
        internal string AfterPath => Path.Combine(Runtime.LiveOptions.SnapshotDir, "after-snapshot-0001.json");
        internal CropTeacherRunStep Step => new(CropTeacherRunStageKinds.RouteHome,
            Source.Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
            string.Empty, "FarmHouse", "FarmHouse", string.Empty, string.Empty, 0, 0, null, "original_route_fixture");

        internal OwnedRouteFixture(bool multiHop = false, string connectorKind = "warp", Action<Fixture>? prepareSource = null,
            bool productExecutor = false)
        {
            Source = new(otherSeeds: 39);
            SetInitialRouteSource(Source, multiHop, connectorKind, prepareSource);
            Runtime = new(Source);
            Runtime.LiveOptions.UseProductExecutor = productExecutor;
            Inputs = new(Source);
            Files = new(Source, Runtime, Inputs);
            Runtime.LiveOptions.RunId = Files.Options.RunId;
            Runtime.LiveOptions.BridgeSnapshotUrl = "http://test/fixture/snapshot";
            Runtime.LiveOptions.ExecutorUrl = "http://test";
            Runtime.LiveOptions.EmitQueueExecutionReceipt = true;
            Runtime.LiveOptions.SnapshotArtifactMode = "plain";
            Runtime.LiveOptions.MaxQueueItemAttempts = 1;
            Directory.CreateDirectory(Runtime.LiveOptions.SnapshotDir);
        }

        internal async Task<CropTeacherManagedSeedOperation> Prepare(CropTeacherManagedSeedOwner owner, int iteration = 1,
            CropTeacherRunStep? step = null, string? runtimeDirectory = null)
        {
            try
            {
                return await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedRouteAsync", Runtime.Client,
                    Runtime.LiveOptions, iteration, step ?? Step, Inputs.Plan, Inputs.Request, owner,
                    runtimeDirectory ?? (iteration == 1 ? Files.Native : Files.Native + "-next"));
            }
            catch (InvalidDataException error) when (File.Exists(QueuePath))
            {
                var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(QueuePath), JsonOptions)!;
                throw new InvalidDataException(error.Message + ":" + queue.Status + ":" + string.Join(",",
                    queue.CompilerDiagnostics.Concat(queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors)))), error);
            }
        }

        internal async Task<JsonObject> EmitNative(int iteration = 1, string? sourcePath = null, Action<SnapshotEnvelope>? change = null)
        {
            var (after, receipt) = BuildNativeResult(iteration, sourcePath, change);
            var afterPath = Path.Combine(Runtime.LiveOptions.SnapshotDir, $"after-snapshot-{iteration:D4}.json");
            var receiptPath = Path.Combine(Runtime.LiveOptions.SnapshotDir, $"execution-{iteration:D4}.json");
            Fixture.Save(afterPath, after);
            Fixture.Save(receiptPath, receipt);
            Runtime.Store.Snapshots[after.StateHash] = after;
            await Task.CompletedTask;
            return new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = afterPath };
        }

        internal (SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt) BuildNativeResult(int iteration = 1, string? sourcePath = null, Action<SnapshotEnvelope>? change = null)
        {
            var before = sourcePath is null ? Source.Before : JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(sourcePath), JsonOptions)!;
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(Path.Combine(Runtime.LiveOptions.SnapshotDir,
                $"crop-teacher-route-{iteration:D4}-compiled-queue.json")), JsonOptions)!;
            var parameters = queue.Items.Single().NormalizedCommand.Parameters;
            var target = parameters.Single(row => row.Name == "expected_target_location").Value;
            var candidate = parameters.Single(row => row.Name == "precondition" && row.Value.StartsWith("candidate_id:", StringComparison.Ordinal))
                .Value["candidate_id:".Length..];
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
            ChangeState(after, "player", node =>
            {
                node["location_id"]!["value"] = target;
                node["tile_x"]!["value"] = 5;
                node["tile_y"]!["value"] = 6;
            });
            ChangeState(after, "farm", node => node["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = target);
            after.GameTick++;
            ChangeState(after, "current_location", node => node["map"]!["value"]!["id"] = target);
            ChangeState(after, "locations", node =>
            {
                node["collision_grid"]!["value"]!["location_id"] = target;
                node["route_connectors"]!["value"]!["location_id"] = target;
                node["route_connectors"]!["value"]!["connectors"] = JsonSerializer.SerializeToNode(new[]
                { new { kind = "warp", tile_x = 6, tile_y = 6, target_location = target == "Town" ? "FarmHouse" : "Farm",
                    target_x = 5, target_y = 6, resolved = true } });
                foreach (var field in new[] { "collision_grid", "route_connectors", "route_action_branch_coverage", "route_graph" })
                    node[field]!["read_at_tick"] = after.GameTick;
            });
            change?.Invoke(after);
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
                Files.Options.RunId, candidate, "traverse_connector");
            return (after, receipt);
        }

        internal Task Rebind(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
            owner.RunSupportingRouteAsync(operation, Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Runtime.Client, Runtime.LiveOptions, diagnostic, request), Files.Post);

        public void Dispose() { Files.Dispose(); Inputs.Dispose(); Runtime.Dispose(); Source.Dispose(); }
    }

    private static void SetInitialRouteSource(Fixture f, bool multiHop, string connectorKind, Action<Fixture>? prepareSource)
    {
        ChangeState(f.Before, "player", node =>
        {
            node["tile_x"] = JsonSerializer.SerializeToNode(new { status = "available", value = 0 });
            node["tile_y"] = JsonSerializer.SerializeToNode(new { status = "available", value = 0 });
        });
        f.Before.State["current_location"] = JsonSerializer.SerializeToElement(new
        {
            map = new { status = "available", value = new { id = "Farm", width = 20, height = 20 } },
            home_context = new { status = "available", value = new { home_location_id = "FarmHouse" } }
        });
        var firstTarget = multiHop ? "Town" : "FarmHouse";
        var locations = f.Before.State.TryGetValue("locations", out var retainedLocations)
            ? JsonNode.Parse(retainedLocations.GetRawText())! : new JsonObject();
        var geometry = JsonSerializer.SerializeToNode(new
        {
            collision_grid = new { status = "available", value = new { location_id = "Farm", width = 20, height = 20, notable_tiles = Array.Empty<object>() } },
            route_action_branch_coverage = new { status = "available", value = new { rows = Array.Empty<object>() } },
            route_connectors = new { status = "available", value = new { location_id = "Farm", connectors = new[]
            { new { kind = connectorKind, tile_x = 1, tile_y = 0, target_location = firstTarget, target_x = 5, target_y = 6, resolved = true } } } },
            route_graph = new { status = "available", value = new { edges = new[]
            {
                new { kind = connectorKind, from_location = "Farm", from_x = 1, from_y = 0, target_location = firstTarget, target_x = 5, target_y = 6, resolved = true },
                new { kind = "warp", from_location = "Town", from_x = 6, from_y = 6, target_location = "FarmHouse", target_x = 5, target_y = 6, resolved = true }
            } } }
        })!;
        foreach (var field in geometry.AsObject())
        {
            var envelope = field.Value!.DeepClone();
            envelope["confidence"] = 1d;
            envelope["source"] = new JsonObject { ["kind"] = "test", ["path"] = "route-fixture" };
            envelope["read_at_tick"] = f.Before.GameTick;
            envelope["adapter"] = "test";
            locations[field.Key] = envelope;
        }
        f.Before.State["locations"] = JsonSerializer.SerializeToElement(locations);
        ChangeState(f.Before, "player", node =>
        {
            foreach (var field in new[] { "location_id", "tile_x", "tile_y" })
            {
                node[field]!["confidence"] = 1d;
                node[field]!["source"] = new JsonObject { ["kind"] = "test", ["path"] = "route-fixture" };
                node[field]!["read_at_tick"] = f.Before.GameTick;
                node[field]!["adapter"] = "test";
            }
        });
        prepareSource?.Invoke(f);
        f.Before.RealTimestamp = "2026-11-08T12:00:00Z";
        foreach (var domain in new[] { "mods", "npcs", "quests", "menus", "modded_state" })
            if (!f.Before.State.ContainsKey(domain)) f.Before.State[domain] = JsonSerializer.SerializeToElement(new JsonObject());
        foreach (var domain in f.Before.State.Keys.ToArray())
        {
            var fields = JsonNode.Parse(f.Before.State[domain].GetRawText())!.AsObject();
            foreach (var field in fields)
                if (field.Value is JsonObject envelope && envelope.ContainsKey("status"))
                {
                    envelope["source"] ??= new JsonObject { ["kind"] = "test", ["path"] = "route-fixture." + domain + "." + field.Key };
                    envelope["adapter"] ??= "test";
                    envelope["read_at_tick"] ??= f.Before.GameTick;
                    envelope["confidence"] ??= 1d;
                }
            f.Before.State[domain] = JsonSerializer.SerializeToElement(fields);
        }
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State);
        f.CropRequest.StateHash = f.Before.StateHash;
        foreach (var request in new[] { f.OtherRequest, f.OwnRequest })
        {
            request.StateHash = f.Before.StateHash;
            foreach (var claim in request.MaterialClaims) claim.StateHash = f.Before.StateHash;
            foreach (var claim in request.CurrencyClaims) claim.StateHash = f.Before.StateHash;
        }
        var crop = new CropCommitmentLedgerService().Upsert(null, f.Before, f.CropRequest, RecordedAt);
        Assert.True(crop.Accepted);
        var other = new ReservationPortfolioLedgerService().Commit(crop.Ledger, f.Before, f.OtherRequest, RecordedAt);
        Assert.True(other.Accepted);
        var own = new ReservationPortfolioLedgerService().Commit(other.Ledger, f.Before, f.OwnRequest, RecordedAt);
        Assert.True(own.Accepted);
        f.Ledger = own.Ledger!;
        Fixture.Save(f.SourcePath, f.Before);
        Fixture.Save(f.LedgerPath, f.Ledger);
    }
}
