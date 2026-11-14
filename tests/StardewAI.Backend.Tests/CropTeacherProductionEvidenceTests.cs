using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherProductionEvidenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProductionEvidenceRebuildsTheOriginalWholeWaveWithoutClaimingSaleOrSaveReadback()
    {
        var f = new Fixture();
        var result = await f.Build();
        var lifecycle = Read<MoneyRevenueCropNativeLifecycleProof>(Assert.Single(result.NativeLifecycleProofPaths));
        var retention = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(result.OutputRetentionProofPaths));
        Assert.True(lifecycle.NativeCropLifecycleVerified); Assert.True(retention.NativeOutputRetentionVerified);
        Assert.Equal(2, retention.MinimumUnits); Assert.Equal(2, retention.InventoryQuantityIncrease);
        Assert.Equal(2, retention.HarvestQueues.Sum(row => row.HarvestActionCount));
        var manifest = Read<JsonObject>(result.ManifestPath);
        Assert.False(manifest["terminal_settlement_verified"]!.GetValue<bool>());
        Assert.False(manifest["native_save_content_readback_verified"]!.GetValue<bool>());
        Assert.False(manifest["formal_product_training_authorized"]!.GetValue<bool>());
        await Assert.ThrowsAsync<IOException>(() => f.Build());
    }

    [Theory]
    [InlineData("missing_day")]
    [InlineData("missing_harvest")]
    [InlineData("changed_input")]
    public async Task ProductionEvidenceKeepsOriginalMissingOrChangedTrajectoryUnproven(string fault)
    {
        var f = new Fixture();
        if (fault == "changed_input") f.ChangeInput();
        else if (fault == "missing_harvest") f.Transitions.RemoveAt(f.Transitions.Count - 1);
        else
        {
            var missingDay = f.Transitions[0].NativeTransition.TotalDay + 1;
            f.Transitions.RemoveAll(row => row.NativeTransition.TotalDay == missingDay);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Build());
        Assert.False(File.Exists(Path.Combine(f.Output, "production-evidence.json")));
    }

    internal sealed class Fixture
    {
        private static readonly Lazy<Fixture> Golden = new(() => new Fixture(null));
        private readonly string root = Path.Combine(Path.GetTempPath(), "managed-production-proof-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
        private readonly string nativePlanPath;
        private readonly string ledgerPath;
        private readonly string finalSnapshotPath;
        internal string Output => Path.Combine(root, "proofs");
        internal string SourcePath => finalSnapshotPath;
        internal string NativePlanPath => nativePlanPath;
        internal string LedgerPath => ledgerPath;
        internal List<CropTeacherManagedProductionTransition> Transitions { get; } = [];

        internal Fixture() : this(Golden.Value) { }
        internal Fixture(int tileCount, string commitmentId) : this(null, tileCount, commitmentId) { }

        private Fixture(Fixture? template, int tileCount = 2, string commitmentId = "declared.managed.production")
        {
            if (template is not null)
            {
                nativePlanPath = template.nativePlanPath; ledgerPath = template.ledgerPath;
                finalSnapshotPath = template.finalSnapshotPath;
                Transitions.AddRange(template.Transitions);
                foreach (var row in template.hashes) hashes.Add(row.Key, row.Value);
                return;
            }
            // Retain one bounded golden trajectory per process, not a full copy per negative case.
            Directory.CreateDirectory(root);
            var source = MoneyCropTeacherFixture.CreateSnapshot("MoneyCropProductionSnapshot");
            var result = new CropCommitmentLedgerService().Upsert(null, source, new CropPlantingCommitmentUpsertRequest
            {
                StateHash = source.StateHash, ExpectedLedgerRevision = 0, CommitmentId = commitmentId,
                SourceDecisionId = "declared.production.adapter.fixture", SeedId = "745", TileCount = tileCount,
                PlantingYear = 2, PlantingSeason = "spring", PlantingDayOfMonth = 20, LocationContext = "outdoor_seasonal"
            }, "2026-11-12T00:00:00Z");
            Assert.True(result.Accepted);
            ledgerPath = Save("ledger", result.Ledger!);
            var request = Save("production-request", new MoneyRevenueCropProductionObligationPlanRequest
                { SnapshotPath = Save("source", source), StrategyLedgerPath = ledgerPath });
            var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(request);
            var production = Save("production-plan", plan); var crop = plan.Commitments.Single();
            Assert.Single(crop.HarvestWaves);
            var xs = Enumerable.Range(5, tileCount).ToArray();
            var tiles = xs.Select(x => new MoneyRevenueCropPlantingTile(x, 6)).ToArray();
            var nativeRequest = Save("native-request", new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = production, LayoutIntents = new[] { new MoneyRevenueCropNativeProductionLayoutIntent(
                    crop.CommitmentId, "Farm", tiles, WateringRefillApproachTile: tileCount == 2 ? new(4, 6) : new(0, 1)) }
            });
            nativePlanPath = Save("native-plan", MoneyRevenueCropNativeProductionRunPlanBuilder.Build(nativeRequest));
            var current = MoneyCropTeacherFixture.CreateSnapshot("MoneyCropPlantingDaySnapshot");
            var daily = MoneyCropTeacherFixture.CreateSnapshot("MoneyCropDailyCapacitySnapshot", crop.PlantingTotalDay, 100);
            current.State["time"] = daily.State["time"]; current.State["locations"] = daily.State["locations"];
            var farmState = JsonNode.Parse(current.State["farm"].GetRawText())!;
            farmState["shipping_bins"] = JsonNode.Parse(daily.State["farm"].GetRawText())!["shipping_bins"]!.DeepClone();
            current.State["farm"] = JsonSerializer.SerializeToElement(farmState);
            var player = JsonNode.Parse(current.State["player"].GetRawText())!.AsObject();
            foreach (var field in JsonNode.Parse(daily.State["player"].GetRawText())!.AsObject()) player[field.Key] = field.Value!.DeepClone();
            current.State["player"] = JsonSerializer.SerializeToElement(player);
            if (tileCount != 2)
            {
                // Opt-in original refill-reader inputs. The two-tile shared golden is unchanged.
                CropTeacherNativeSupportFixture.CompleteResources(current, 0);
                player = JsonNode.Parse(current.State["player"].GetRawText())!.AsObject();
                player["inventory"] = JsonSerializer.SerializeToNode(new { status = "available", value = new[]
                { new { slot_index = 0, item_id = "745", qualified_item_id = "(O)745", stack = 40, quality = 0, is_empty = false } } });
                current.State["player"] = JsonSerializer.SerializeToElement(player);
                var location = JsonNode.Parse(current.State["current_location"].GetRawText())!;
                location["map"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { id = "Farm", width = 100, height = 100 } });
                location["objects"] = JsonSerializer.SerializeToNode(new { status = "available", value = Array.Empty<object>() });
                location["terrain_features"] = JsonSerializer.SerializeToNode(new { status = "available", value = Array.Empty<object>() });
                current.State["current_location"] = JsonSerializer.SerializeToElement(location);
            }
            current.StateHash = SnapshotHash.ComputeStateHash(current.State);
            var nativeTransitions = new List<MoneyRevenueCropNativeLifecycleTransitionRequest>(); var sequence = 0;
            void Add(int day, string option, string target = "")
            {
                object[] args = { root, nativeTransitions, current, day, sequence, option, xs, target, false };
                current = MoneyCropTeacherFixture.CreateSnapshot("AddLifecycleTransition", args); sequence = (int)args[4];
                if (option == "executor.harvest_crop")
                {
                    var playerState = JsonNode.Parse(current.State["player"].GetRawText())!;
                    foreach (var row in playerState["inventory"]!["value"]!.AsArray().OfType<JsonObject>())
                        if (row["qualified_item_id"]?.GetValue<string>() == "(O)400") row["can_be_shipped"] = true;
                    current.State["player"] = JsonSerializer.SerializeToElement(playerState);
                    current.StateHash = SnapshotHash.ComputeStateHash(current.State);
                    var native = nativeTransitions[^1];
                    File.WriteAllText(native.AfterSnapshotPath, JsonSerializer.Serialize(current, JsonOptions));
                    var queue = Read<ActionQueueEnvelope>(native.QueuePath); var before = Read<SnapshotEnvelope>(native.BeforeSnapshotPath);
                    var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before,
                        current, native.RunId, native.SelectedCandidateId, "harvest_crop");
                    File.WriteAllText(native.ExecutionReceiptPath, JsonSerializer.Serialize(receipt, JsonOptions));
                }
            }
            for (var day = crop.PlantingTotalDay; day <= crop.HarvestWaves.Single().HarvestTotalDay; day++)
            {
                if (day > crop.PlantingTotalDay) Add(day, "executor.traverse_connector", "Farm");
                if (day == crop.PlantingTotalDay) Add(day, "executor.plant_seed");
                if (crop.RequiredWateringTotalDays.Contains(day)) Add(day, "executor.water_crop");
                if (day == crop.HarvestWaves.Single().HarvestTotalDay) Add(day, "executor.harvest_crop");
                else { Add(day, "executor.traverse_connector", "FarmHouse"); Add(day, "executor.sleep"); }
            }
            finalSnapshotPath = nativeTransitions[^1].AfterSnapshotPath;
            for (var index = 0; index < nativeTransitions.Count; index++)
                Transitions.Add(new(index + 1, index == 0 ? "owned_seed_planting" : "declared_original_transition", nativeTransitions[index]));
            foreach (var path in Directory.EnumerateFiles(root)) hashes[path] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }

        internal Task<CropTeacherManagedProductionEvidence> Build()
        {
            var trace = (CropTeacherManagedProductionTrace)typeof(CropTeacherManagedProductionTrace).GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(new object[]
                { nativePlanPath, finalSnapshotPath, ledgerPath, Transitions.ToArray(), hashes });
            var builder = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!;
            return (Task<CropTeacherManagedProductionEvidence>)builder.GetMethod("BuildAsync", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { trace, Output, (Func<string, string, Task>)((path, json) => File.WriteAllTextAsync(path, json)) })!;
        }
        internal void ChangeInput()
        {
            Directory.CreateDirectory(root);
            var first = Transitions[0]; var original = first.NativeTransition.QueuePath;
            var changed = Path.Combine(root, "tampered-queue.json");
            File.Copy(original, changed); File.AppendAllText(changed, " ");
            hashes[changed] = hashes[original];
            Transitions[0] = first with { NativeTransition = first.NativeTransition with { QueuePath = changed } };
        }
        private string Save<T>(string name, T value)
        {
            var path = Path.Combine(root, name + ".json"); File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions)); return path;
        }
    }
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)!;
}
