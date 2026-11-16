using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class ObservedTerminalOwnerFixture : IAsyncDisposable
    {
        internal NativeVariantTerminalFixture F { get; } = new(interleaved: true, nativeWire: true);
        internal CropTeacherManagedSeedOwner Owner { get; private set; } = null!;
        internal CropTeacherManagedProductionEvidence Evidence { get; private set; } = null!;
        internal CropTeacherTerminalShippingTarget OriginalTarget { get; private set; } = null!;
        internal string InitialPath { get; private set; } = "";
        internal static async Task<ObservedTerminalOwnerFixture> Create(int prefixCount)
        {
            var fixture = new ObservedTerminalOwnerFixture();
            try { await fixture.Initialize(prefixCount); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task Initialize(int count)
        {
            F.Shared.Runtime.LiveOptions.SkipTraining = true;
            F.Shared.Runtime.LiveOptions.SaveIsolationPath = "isolated-single-till-fixture";
            Owner = await F.Shared.Files.Open(); InitialPath = Owner.CurrentSnapshotPath;
            var proof = LifecycleRead<MoneyRevenueCropOutputRetentionProof>(F.Target.RetentionProofPath);
            proof.AfterSnapshotPath = InitialPath; proof.AfterSnapshotSha256 = Hash(InitialPath);
            Fixture.Save(F.Target.RetentionProofPath, proof);
            OriginalTarget = F.Target with { RetentionProofSha256 = Hash(F.Target.RetentionProofPath) };
            for (var index = 0; index < count; index++)
            {
                var paths = await Emit(OriginalTarget with { AcceptedDepositCount = index }, index);
                var native = new MoneyRevenueCropNativeLifecycleTransitionRequest("shared_source_return", OriginalTarget.TotalDay,
                    paths.QueuePath, Owner.CurrentSnapshotPath, paths.ReceiptPath, paths.AfterSnapshotPath,
                    F.Shared.Files.Options.RunId, F.Shared.Files.Options.ExecutorVersion, paths.CandidateId);
                var operation = await Owner.AdmitSharedSourceReturnAsync(native, F.Shared.Files.Native + "-observed-" + index);
                await Owner.RunSharedSourceReturnAsync(operation, F.Shared.Runtime.Client, (diagnostic, request) =>
                    InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", F.Shared.Runtime.Client,
                        F.Shared.Runtime.LiveOptions, diagnostic, request), F.Shared.Files.Post);
            }
            var trace = await Owner.ReadJointProductionTraceAsync();
            var hashes = trace.CropTrace.InputHashes.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
            hashes[F.Target.RetentionProofPath] = Hash(F.Target.RetentionProofPath);
            var root = Path.Combine(F.Shared.Files.OwnerRoot, "declared-observed-production"); Directory.CreateDirectory(root);
            var path = Path.Combine(root, "production-evidence.json");
            // Deliberate calibration certificate: tests prefix/selection/owner integration, not a real harvest lifecycle.
            var manifest = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_production_evidence.v1",
                ["native_run_plan_path"] = F.Shared.Files.Options.NativeRunPlanPath,
                ["accepted_snapshot_path"] = Owner.CurrentSnapshotPath, ["accepted_ledger_path"] = Owner.CurrentLedgerPath,
                ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(hashes, JsonOptions),
                ["owner_transitions"] = JsonSerializer.SerializeToNode(trace.CropTrace.Transitions, JsonOptions),
                ["owner_shared_source_returns"] = JsonSerializer.SerializeToNode(trace.SharedSourceReturns, JsonOptions),
                ["planting_layout_proof_paths"] = new JsonArray(), ["daily_capacity_proof_paths"] = new JsonArray(),
                ["native_lifecycle_proof_paths"] = new JsonArray(), ["output_retention_proof_paths"] = new JsonArray(F.Target.RetentionProofPath),
                ["native_crop_lifecycle_verified"] = true, ["whole_wave_output_retention_verified"] = true,
                ["terminal_settlement_verified"] = false, ["formal_product_training_authorized"] = false
            };
            if (count > 0) manifest["observed_shipping_prefixes"] = JsonSerializer.SerializeToNode(new[]
            {
                new CropTeacherObservedShippingPrefix("crop_teacher_observed_shipping_prefix.v1", F.Target.RetentionProofPath,
                    InitialPath, trace.SharedSourceReturns.Select(row => row.NativeTransition with { NativeSharedSource = row.SourceReference }).ToArray())
            }, JsonOptions);
            Fixture.Save(path, manifest);
            Evidence = new(path, [], [], [], [F.Target.RetentionProofPath], Hash(path));
        }

        internal async Task<object> Select()
        {
            var task = (Task)typeof(CropTeacherManagedSeedOwner).GetMethod("SelectJointTerminalShippingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Owner, [Evidence])!;
            await task.ConfigureAwait(false); return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }
        internal static CropTeacherTerminalShippingTarget Target(object selection, int count) =>
            (CropTeacherTerminalShippingTarget)selection.GetType().GetMethod("CreateTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(selection, [count])!;
        internal JsonObject ReadIntent(CropTeacherManagedSeedOperation operation) =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(F.Shared.Files.OwnerRoot, $"operation-{operation.Sequence:D6}-intent.json")))!.AsObject();
        internal async Task Reopen()
        {
            await Owner.DisposeAsync(); Owner = await F.Shared.Files.Open();
            await Owner.RestoreHeadAsync(F.Shared.Runtime.Client, F.Shared.Files.Recovery, F.Shared.Files.Post);
        }

        internal sealed record Emitted(string ModelPath, string QueuePath, string ReceiptPath, string AfterSnapshotPath, string CandidateId);
        internal Emitted Prepare(CropTeacherTerminalShippingTarget target, int ordinal)
        {
            var beforePath = Owner.CurrentSnapshotPath; var before = LifecycleRead<SnapshotEnvelope>(beforePath);
            var model = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(before, target);
            var queue = new ActionQueueCompiler().Compile(model, before, ReadLedger(Owner.CurrentLedgerPath));
            var item = Assert.Single(queue.Items);
            var stem = Path.Combine(F.Root, "observed-native-" + ordinal);
            var paths = new Emitted(stem + "-model.json", stem + "-queue.json", stem + "-receipt.json", stem + "-after.json",
                model.ModelOutputId["model-output:".Length..]);
            Fixture.Save(paths.ModelPath, model); Fixture.Save(paths.QueuePath, queue);
            return paths;
        }

        internal async Task<Emitted> Emit(CropTeacherTerminalShippingTarget target, int ordinal, Emitted? prepared = null)
        {
            var paths = prepared ?? Prepare(target, ordinal);
            var beforePath = Owner.CurrentSnapshotPath; var before = LifecycleRead<SnapshotEnvelope>(beforePath);
            var queue = LifecycleRead<ActionQueueEnvelope>(paths.QueuePath); var item = Assert.Single(queue.Items);
            var stem = paths.ReceiptPath[..^"-receipt.json".Length];
            var after = F.Deposit(before, ordinal, target); Fixture.Save(paths.AfterSnapshotPath, after);
            F.Shared.Runtime.Store.Snapshots[after.StateHash] = after;
            SimulatedFactChange[] facts =
            [
                new() { Path = "player.inventory." + target.QualifiedItemId + ".count", Before = (6 - ordinal).ToString(), After = (5 - ordinal).ToString() },
                new() { Path = "farm.shipping_bin." + target.QualifiedItemId + ".count", Before = ordinal.ToString(), After = (ordinal + 1).ToString() }
            ];
            var wire = await NativeQueueWireFixture.WriteAsync(F.Shared.Runtime.LiveOptions, paths.QueuePath, beforePath,
                paths.AfterSnapshotPath, 0, "ship_inventory_item_to_bin", facts, stem + "-result.json", 810 + ordinal);
            var receipt = F.Receipt(queue, before, after, paths.CandidateId, ordinal);
            receipt.StepResults[0].AfterSnapshotPath = paths.AfterSnapshotPath;
            receipt.StepResults[0].ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions);
            var aggregate = JsonSerializer.SerializeToNode(receipt, JsonOptions)!.AsObject();
            aggregate["after_snapshot_path"] = paths.AfterSnapshotPath;
            NativeQueueWireFixture.Bind(aggregate, 0, paths.AfterSnapshotPath, facts, wire);
            Fixture.Save(paths.ReceiptPath, aggregate);
            return paths;
        }

        internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        public async ValueTask DisposeAsync() { if (Owner is not null) await Owner.DisposeAsync(); F.Dispose(); }
    }
}
