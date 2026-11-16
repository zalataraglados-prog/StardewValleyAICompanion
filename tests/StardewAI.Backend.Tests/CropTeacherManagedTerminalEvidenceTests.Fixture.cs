using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<Task<Golden>> Shared = new(Golden.Create);
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)!;
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // One immutable declared original whole-wave/settlement golden. It is not an actual managed/native run.
    private sealed record Golden(CropTeacherProductionEvidenceTests.Fixture ProductionFixture,
        CropTeacherManagedProductionEvidence Production, CropTeacherTerminalShippingTarget Target,
        CropTeacherManagedProductionTransition[] Transitions, Dictionary<string, string> Hashes, string FinalPath, string FinalLedgerPath)
    {
        internal static async Task<Golden> Create()
        {
            var f = new CropTeacherProductionEvidenceTests.Fixture();
            var production = await f.Build();
            var retentionPath = Assert.Single(production.OutputRetentionProofPaths);
            var retention = Read<MoneyRevenueCropOutputRetentionProof>(retentionPath);
            var root = Path.Combine(Path.GetTempPath(), "managed-terminal-golden-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var proofPath = MoneyCropTeacherFixture.Create<string>("VerifyMoneyCropShippingTerminalSettlementProof",
                root, retention.CropProductionPlanPath, retentionPath);
            var proof = Read<MoneyRevenueCropTerminalSettlementProof>(proofPath);
            var request = Read<MoneyRevenueCropTerminalSettlementProofRequest>(proof.RequestPath);
            var recurrence = Read<MoneyRevenueRecurrenceProofManifest>(request.RevenueRecurrenceManifestPath).Transitions.Single();
            var initial = Read<SnapshotEnvelope>(retention.AfterSnapshotPath);
            var item = initial.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray()
                .Single(row => row.GetProperty("qualified_item_id").GetString() == retention.HarvestItemQualifiedId);
            var bin = initial.State["farm"].GetProperty("shipping_bins").GetProperty("value").EnumerateArray().Single();
            var target = new CropTeacherTerminalShippingTarget(retentionPath, Hash(retentionPath), initial.SaveId.Value!, initial.PlayerId.Value!,
                initial.GameVersion, retention.HarvestTotalDay, retention.HarvestItemQualifiedId, item.GetProperty("slot_index").GetInt32(),
                item.GetProperty("quality").GetInt32(), item.GetProperty("sell_to_store_price").GetInt32(),
                bin.GetProperty("tile_x").GetInt32(), bin.GetProperty("tile_y").GetInt32(),
                bin.GetProperty("interaction_stand_tile_x").GetInt32(), bin.GetProperty("interaction_stand_tile_y").GetInt32(),
                item.GetProperty("stack").GetInt32(), retention.MinimumUnits, retention.MinimumUnits);
            var transitions = f.Transitions.ToList();
            var previous = retention.AfterSnapshotPath;
            void Add(MoneyRevenueTerminalQueueTransitionProof value, string operation, string kind)
            {
                // The original helper uses its own JSON formatting for duplicate before snapshots.
                // Bind the exact accepted predecessor only when the entire parsed envelope agrees.
                Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
                    Read<System.Text.Json.Nodes.JsonNode>(previous), Read<System.Text.Json.Nodes.JsonNode>(value.BeforeSnapshotPath)));
                var receipt = Read<QueueExecutionReceiptEnvelope>(value.ExecutionReceiptPath);
                receipt.RunId = "declared-managed-terminal-run";
                File.WriteAllText(value.ExecutionReceiptPath, JsonSerializer.Serialize(receipt, JsonOptions));
                transitions.Add(new(transitions.Count + 1, operation, new(kind, target.TotalDay, value.QueuePath,
                    previous, value.ExecutionReceiptPath, value.AfterSnapshotPath, receipt.RunId, value.ExecutorVersion, value.SelectedCandidateId)));
                previous = value.AfterSnapshotPath;
            }
            foreach (var row in request.RouteTransitions) Add(row, "terminal_shipping", "shipping_approach");
            foreach (var row in request.ShippingDepositTransitions) Add(row, "terminal_shipping", "shipping_deposit");
            foreach (var row in request.SettlementRouteTransitions) Add(row, "supporting_route", "supporting_route_to_sleep");
            Add(new MoneyRevenueTerminalQueueTransitionProof
            {
                QueuePath = recurrence.QueuePath, BeforeSnapshotPath = recurrence.BeforeSnapshotPath,
                ExecutionReceiptPath = recurrence.ExecutionReceiptPath, AfterSnapshotPath = recurrence.AfterSnapshotPath,
                RunId = recurrence.RunId, ExecutorVersion = recurrence.ExecutorVersion, SelectedCandidateId = recurrence.SelectedCandidateId
            }, "overnight_control", "native_day_rollover");
            var hashes = Read<JsonElement>(production.ManifestPath).GetProperty("input_artifact_hashes")
                .EnumerateObject().ToDictionary(row => row.Name, row => row.Value.GetString()!, StringComparer.OrdinalIgnoreCase);
            foreach (var row in transitions)
                foreach (var path in new[] { row.NativeTransition.QueuePath, row.NativeTransition.BeforeSnapshotPath,
                    row.NativeTransition.ExecutionReceiptPath, row.NativeTransition.AfterSnapshotPath }) hashes[path] = Hash(path);
            foreach (var path in new[] { f.NativePlanPath, f.LedgerPath, production.ManifestPath, retentionPath }) hashes[path] = Hash(path);
            var finalLedgerPath = Path.Combine(root, "declared-final-ledger.json");
            var finalLedger = Read<StrategyCommitmentLedger>(f.LedgerPath);
            finalLedger.SourceStateHash = Read<SnapshotEnvelope>(previous).StateHash;
            File.WriteAllText(finalLedgerPath, JsonSerializer.Serialize(finalLedger, JsonOptions));
            hashes[finalLedgerPath] = Hash(finalLedgerPath);
            return new(f, production, target, transitions.ToArray(), hashes, previous, finalLedgerPath);
        }
    }

    private sealed class Case(Golden golden)
    {
        internal CropTeacherManagedProductionEvidence Production = golden.Production;
        internal CropTeacherTerminalShippingTarget Target = golden.Target;
        internal List<CropTeacherManagedProductionTransition> Transitions = golden.Transitions.ToList();
        internal Dictionary<string, string> Hashes = new(golden.Hashes, StringComparer.OrdinalIgnoreCase);
        internal string FinalPath = golden.FinalPath;
        internal string Output = Path.Combine(Path.GetTempPath(), "managed-terminal-result-" + Guid.NewGuid().ToString("N"));

        internal async Task<CropTeacherManagedTerminalEvidence> Build()
        {
            var trace = (CropTeacherManagedProductionTrace)typeof(CropTeacherManagedProductionTrace).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single().Invoke(new object[] { golden.ProductionFixture.NativePlanPath, FinalPath, golden.FinalLedgerPath, Transitions.ToArray(), Hashes });
            var type = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherTerminalSettlementEvidence")!;
            await Task.Yield();
            Task<CropTeacherManagedTerminalEvidence> task;
            try
            {
                task = (Task<CropTeacherManagedTerminalEvidence>)type.GetMethod("BuildAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { trace, Production, Target, Output,
                        (Func<string, string, Task>)((path, json) => File.WriteAllTextAsync(path, json)) })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
            return await task;
        }
    }
}
