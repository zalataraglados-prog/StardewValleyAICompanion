using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    private static readonly Lazy<Task<Golden>> PartialObservedGolden = new(() => CreateObservedGolden(1, true));
    private static readonly Lazy<Task<Golden>> CompleteObservedGolden = new(() => CreateObservedGolden(2, false));

    private static async Task<Golden> CreateObservedGolden(int prefixCount, bool controls)
    {
        var original = await CreateControlGolden(false, controls, nativeWireSurface: true);
        var f = original.ProductionFixture;
        var root = Path.Combine(Path.GetDirectoryName(original.FinalPath)!, "observed-prefix"); Directory.CreateDirectory(root);
        var plan = Read<MoneyRevenueCropNativeProductionRunPlan>(f.NativePlanPath);
        var shipping = original.Transitions.Where(row => row.OperationKind == "terminal_shipping" &&
            row.NativeTransition.TransitionKind == "shipping_deposit").Take(prefixCount).ToArray();
        Assert.Equal(prefixCount, shipping.Length);
        var shared = new List<CropTeacherManagedSharedSourceReturn>();
        var hashes = f.Trace().InputHashes.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        var options = new LiveTrainingOptions { Root = root, RunId = "declared-observed-terminal", SkipTraining = true,
            SaveIsolationPath = "isolated-observed-terminal-fixture" };
        string Save<T>(string name, T value)
        {
            var path = Path.Combine(root, name + ".json"); File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions)); return path;
        }
        foreach (var row in shipping)
        {
            var native = row.NativeTransition with { TransitionKind = "shared_source_return" };
            var before = Read<SnapshotEnvelope>(native.BeforeSnapshotPath);
            var receipt = Read<QueueExecutionReceiptEnvelope>(native.ExecutionReceiptPath);
            var facts = receipt.StepResults[0].ChangedFacts.EnumerateArray().Select(fact => new SimulatedFactChange
            {
                Path = fact.GetProperty("path").GetString()!, Before = fact.GetProperty("before").ToString(), After = fact.GetProperty("after").ToString()
            }).ToArray();
            var wire = await NativeQueueWireFixture.WriteAsync(options, native.QueuePath, native.BeforeSnapshotPath,
                native.AfterSnapshotPath, 0, "ship_inventory_item_to_bin", facts, Path.Combine(root, "wire-" + row.OwnerSequence + ".json"), row.OwnerSequence);
            var aggregate = JsonSerializer.SerializeToNode(receipt, JsonOptions)!.AsObject();
            aggregate["after_snapshot_path"] = native.AfterSnapshotPath;
            NativeQueueWireFixture.Bind(aggregate, 0, native.AfterSnapshotPath, facts, wire);
            File.WriteAllText(native.ExecutionReceiptPath, aggregate.ToJsonString(JsonOptions));
            var ledger = Read<StrategyCommitmentLedger>(f.LedgerPath); ledger.SourceStateHash = before.StateHash;
            var ledgerPath = Save("source-ledger-" + row.OwnerSequence, ledger);
            var step = "declared-original-shipping-" + row.OwnerSequence;
            var scope = CropTeacherSharedSourceReturn.OpenNativeWork(plan.RequestPath, native.BeforeSnapshotPath,
                ledgerPath, plan.CoveredCommitmentIds, native, step);
            var reference = new MoneyRevenueCropNativeSharedSourceReference(plan.RequestPath, ledgerPath,
                plan.CoveredCommitmentIds, step, scope.InputHashes) { AppliedNativeWorkQueue = true };
            shared.Add(new(row.OwnerSequence, step, native) { SourceReference = reference });
            foreach (var input in scope.InputHashes) hashes[input.Key] = input.Value;
        }
        var prefixSequences = shipping.Select(row => row.OwnerSequence).ToHashSet();
        var headPath = shipping[^1].NativeTransition.AfterSnapshotPath;
        var headLedger = Read<StrategyCommitmentLedger>(f.LedgerPath); headLedger.SourceStateHash = Read<SnapshotEnvelope>(headPath).StateHash;
        var headLedgerPath = Save("accepted-prefix-ledger", headLedger); hashes[headLedgerPath] = Hash(headLedgerPath);
        var owned = original.Transitions.Where(row => row.OwnerSequence <= shipping[^1].OwnerSequence && !prefixSequences.Contains(row.OwnerSequence)).ToArray();
        foreach (var row in owned)
            foreach (var path in new[] { row.NativeTransition.QueuePath, row.NativeTransition.BeforeSnapshotPath,
                row.NativeTransition.ExecutionReceiptPath, row.NativeTransition.AfterSnapshotPath }) hashes[path] = Hash(path);
        var trace = NewTrace(f.NativePlanPath, headPath, headLedgerPath, owned, hashes);
        var builder = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!;
        var production = await (Task<CropTeacherManagedProductionEvidence>)builder.GetMethod("BuildJointNativeWorkAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new CropTeacherManagedJointProductionTrace(trace, shared), Path.Combine(root, "production"),
                (Func<string, string, Task>)((path, json) => File.WriteAllTextAsync(path, json)), false])!;
        var retentionPath = Assert.Single(production.OutputRetentionProofPaths);
        var target = original.Target with { RetentionProofPath = retentionPath, RetentionProofSha256 = Hash(retentionPath),
            NativeVariants = original.Target.NativeVariants! with { SchemaVersion = "crop_teacher_native_terminal_variants.v2",
                DepositOrder = new(original.Target.SlotIndex, new(original.Target.SlotIndex)) } };
        var allHashes = new Dictionary<string, string>(original.Hashes, StringComparer.OrdinalIgnoreCase);
        foreach (var row in hashes) allHashes[row.Key] = row.Value;
        foreach (var row in Read<JsonElement>(production.ManifestPath).GetProperty("input_artifact_hashes").EnumerateObject())
            allHashes[row.Name] = row.Value.GetString()!;
        allHashes[production.ManifestPath] = Hash(production.ManifestPath); allHashes[retentionPath] = Hash(retentionPath);
        return original with { Production = production, Target = target,
            Transitions = original.Transitions.Where(row => !prefixSequences.Contains(row.OwnerSequence)).ToArray(), Hashes = allHashes };
    }

    private static CropTeacherManagedProductionTrace NewTrace(string plan, string head, string ledger,
        CropTeacherManagedProductionTransition[] rows, IReadOnlyDictionary<string, string> hashes) =>
        (CropTeacherManagedProductionTrace)typeof(CropTeacherManagedProductionTrace).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single().Invoke([plan, head, ledger, rows, hashes]);
}
