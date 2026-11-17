using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Declared production leaf plus original owner/route/overnight protocols, not whole-game acceptance.
    private sealed class GlobalContinuationFixture : IAsyncDisposable
    {
        internal TerminalOutcomeFixture F { get; } = new(quantity: 1, prepareSnapshot: snapshot =>
            ChangeState(snapshot, "player", fields => fields["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = 100 })));
        internal CropTeacherManagedSeedOwner Owner { get; private set; } = null!;
        internal CropTeacherManagedSeedOperation Night { get; private set; } = null!;
        internal CropTeacherTerminalShippingTarget Target { get; private set; } = null!;
        internal CropTeacherManagedProductionEvidence Production { get; private set; } = null!;
        internal object Lifetime { get; private set; } = null!;
        internal LiveTrainingOptions Options => F.Shared.Runtime.LiveOptions;
        private string SaveFile => Path.Combine(Options.SaveIsolationPath, Options.SaveSlot, "declared-native-save.dat");

        internal async Task Initialize()
        {
            Options.UseManagedCropTeacher = true; Options.ContinueAfterCropTeacherRun = true;
            Options.UseDailyPlan = true; Options.RequireNativeSaveBoundary = true;
            Options.ArtifactRetentionMode = "stop";
            Options.Root = F.Shared.Source.RuntimePath;
            Directory.CreateDirectory(Path.GetDirectoryName(Options.ProgressLogPath)!);
            Options.SaveIsolationPath = Path.Combine(F.Shared.Source.RuntimePath, "declared-native-saves"); Options.SaveSlot = "DeclaredSlot";
            Directory.CreateDirectory(Path.GetDirectoryName(SaveFile)!); File.WriteAllText(SaveFile, "before-night");
            Owner = await F.Shared.Files.Open(F.Shared.Files.Options with
            { NativeSaveBinding = new(Options.SaveIsolationPath, Options.SaveSlot) });
            var proof = LifecycleRead<MoneyRevenueCropOutputRetentionProof>(F.ProofPath);
            proof.AfterSnapshotPath = Owner.CurrentSnapshotPath;
            proof.AfterSnapshotSha256 = SavedResourceFixture.Hash(Owner.CurrentSnapshotPath);
            Fixture.Save(F.ProofPath, proof);
            var reference = DeclaredTerminalProductionReference(Owner, F, manifest =>
            {
                var paths = new[] { F.Shared.Inputs.PlanPath, Owner.CurrentSnapshotPath, Owner.CurrentLedgerPath, F.ProofPath };
                manifest["input_artifact_hashes"] = JsonSerializer.SerializeToNode(
                    paths.ToDictionary(path => path, SavedResourceFixture.Hash), JsonOptions);
            });
            Production = new(reference.ManifestPath, [], [], [], [F.ProofPath], reference.ManifestSha256);
            var selectionTask = (Task)typeof(CropTeacherManagedSeedOwner).GetMethod("SelectTerminalShippingAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Owner, [Production, false])!;
            await selectionTask;
            var selection = selectionTask.GetType().GetProperty("Result")!.GetValue(selectionTask)!;
            Target = ObservedTerminalOwnerFixture.Target(selection, 0);
            var type = typeof(LiveTrainingOptions).Assembly.GetType("Program+CropTeacherOwnerLifetime", true)!;
            Lifetime = Activator.CreateInstance(type, nonPublic: true)!;
            type.GetMethod("Attach", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Lifetime, [Owner, null]);
            type.GetMethod("AttachTerminal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Lifetime, [Production, selection]);

            var deposit = await Owner.CompileAndAdmitOriginalTerminalAsync(Target, () => Task.FromResult(
                new CropTeacherOriginalTerminalCompilation(F.ModelPath, F.QueuePath, F.Shared.ReceiptPath,
                    F.Shared.AfterPath, F.Shared.Files.Native)), reference);
            await Owner.ExecuteOriginalTerminalAsync(deposit, F.Shared.Runtime.Client, () => Task.FromResult(EmitTerminal(F)));
            await RebindTerminal(Owner, deposit, F);
            Target = Target with { AcceptedDepositCount = 1 };

            var source = Owner.CurrentSnapshotPath;
            var route = await F.Shared.Prepare(Owner, 2);
            await Owner.ExecuteOriginalRouteAsync(route, F.Shared.Runtime.Client, () => F.Shared.EmitNative(2, source, after =>
            {
                ChangeState(after, "current_location", fields => fields["home_context"] = JsonSerializer.SerializeToNode(new
                { status = "available", value = new { home_location_id = "FarmHouse", home_available = true,
                    current_location_is_home = true, bed_tile_has_bed = true, bed_tile_x = 6, bed_tile_y = 6 } }));
                ChangeState(after, "menus", fields =>
                {
                    fields["active_menu"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { is_open = false, type = "none" } });
                    fields["menu_specific_state"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { } });
                    fields["sleep_prompt_context"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { prompt_open = false } });
                });
                CropTeacherNativeSupportFixture.CompleteEnvelopeMetadata(after, "declared-global-continuation");
                after.StateHash = SnapshotHash.ComputeStateHash(after.State);
            }));
            await F.Shared.Rebind(Owner, route);
            var step = new CropTeacherRunStep(CropTeacherRunStageKinds.NativeSleep, Target.TotalDay,
                "", "FarmHouse", "FarmHouse", "", "", 0, 0, null, "declared_terminal_night");
            Night = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedOvernightAsync",
                F.Shared.Runtime.Client, Options, 3, step, F.Shared.Inputs.Plan, F.Shared.Inputs.Request, Owner,
                F.Shared.Files.Native + "-terminal-night");
        }

        internal async Task CompleteNight()
        {
            await Owner.ExecuteOriginalOvernightAsync(Night, F.Shared.Runtime.Client, () =>
            {
                var before = LifecycleRead<SnapshotEnvelope>(Owner.CurrentSnapshotPath);
                var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
                after.GameTick = before.GameTick + 1;
                after.InGameTime.Value = 600;
                ChangeState(after, "time", fields =>
                {
                    fields["total_days"]!["value"] = Target.TotalDay + 1;
                    fields["day"]!["value"] = fields["day"]!["value"]!.GetValue<int>() + 1;
                    fields["time"]!["value"] = 600;
                });
                ChangeState(after, "player", fields =>
                {
                    fields["money"]!["value"] = fields["money"]!["value"]!.GetValue<int>() + 50;
                    fields["total_money_earned"]!["value"] = fields["total_money_earned"]!["value"]!.GetValue<uint>() + 50U;
                });
                ChangeState(after, "farm", fields =>
                {
                    var bin = fields["shipping_bins"]!["value"]!.AsArray().Single(row =>
                        row!["tile_x"]!.GetValue<int>() == Target.BinTileX && row["tile_y"]!.GetValue<int>() == Target.BinTileY)!;
                    bin["contents"] = new JsonArray(); bin["pending_settlement_value"] = 0;
                    bin["contents_total_count"] = 0; bin["contents_distinct_item_count"] = 0;
                    bin["contents_distinct_settlement_variant_count"] = 0; bin["contents_signature"] = "declared-empty-shipping-scope";
                });
                var queue = LifecycleRead<ActionQueueEnvelope>(Path.Combine(F.Root, "crop-teacher-sleep-0003-compiled-queue.json"));
                var candidate = queue.Items.Single().NormalizedCommand.Parameters.Single(row => row.Name == "precondition" &&
                    row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
                var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue,
                    before, after, Options.RunId, candidate, "sleep");
                var receiptPath = Path.Combine(F.Root, "execution-0003.json");
                var afterPath = Path.Combine(F.Root, "after-snapshot-0003.json");
                receipt.StepResults.Single().AfterSnapshotPath = afterPath;
                Fixture.Save(receiptPath, receipt); Fixture.Save(afterPath, after);
                F.Shared.Runtime.Store.Snapshots[after.StateHash] = after;
                File.WriteAllText(SaveFile, "after-native-night");
                return Task.FromResult(new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = afterPath });
            });
            await Owner.RunOvernightAsync(Night, F.Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", F.Shared.Runtime.Client, Options,
                    diagnostic, request), F.Shared.Files.Post);
        }

        internal Task<JsonObject> ReadBoundary(CropTeacherTerminalShippingTarget? target = null) =>
            (Task<JsonObject>)typeof(CropTeacherManagedSeedOwner).GetMethod("ReadTerminalGlobalContinuationAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Owner, [Night, target ?? Target, Production])!;
        internal Task<string?> Continue() => InvokeLive<Task<string?>>("ContinueAfterManagedCropRunAsync", Options, 3, Lifetime, Night);
        public async ValueTask DisposeAsync()
        {
            if (Lifetime is IAsyncDisposable lifetime) await lifetime.DisposeAsync();
            else if (Owner is not null) await Owner.DisposeAsync();
            // Keep this diagnostic fixture's files, including its native boundary, for failure analysis.
            F.Shared.Runtime.Dispose();
        }
    }
}
