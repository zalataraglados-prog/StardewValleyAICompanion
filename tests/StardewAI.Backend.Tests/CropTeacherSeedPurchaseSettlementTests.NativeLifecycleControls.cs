using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeTerminalControlTailReusesExactControlChecksWithoutAcceptingMovementMissingOrMisboundTails()
    {
        var f = new LifecycleControlsFixture(); await f.Build();
        var row = f.Rows[^1] with { BeforeSnapshotPath = f.Rows[^2].AfterSnapshotPath };
        f.Rows[^1] = row;
        var verified = Assert.Single(MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(row.BeforeSnapshotPath, [row], row.AfterSnapshotPath));
        Assert.Equal("executor.wait_ticks", Assert.Single(verified.OptionIds));
        foreach (var fault in new[] { "missing", "duplicate", "candidate", "movement", "head", "source" })
        {
            var rows = fault == "missing" ? Array.Empty<MoneyRevenueCropNativeLifecycleTransitionRequest>() :
                fault == "duplicate" ? [row, row] : new[] { fault == "candidate" ? row with { SelectedCandidateId = "foreign" } :
                    fault == "movement" ? f.Rows[^3] : row };
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(
                fault == "source" ? row.AfterSnapshotPath : row.BeforeSnapshotPath, rows,
                fault == "head" ? row.BeforeSnapshotPath : row.AfterSnapshotPath));
        }
        // Declared metadata binding only; this does not fabricate a production certificate or native sale.
        var source = LifecycleRead<SnapshotEnvelope>(row.BeforeSnapshotPath);
        var hashes = f.Rows.SelectMany(value => new[] { value.QueuePath, value.BeforeSnapshotPath, value.ExecutionReceiptPath, value.AfterSnapshotPath })
            .Distinct().ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        var retention = new MoneyRevenueCropOutputRetentionProof { AfterSnapshotPath = row.BeforeSnapshotPath,
            AfterStateHash = source.StateHash, AfterSnapshotSha256 = hashes[row.BeforeSnapshotPath], HarvestTotalDay = row.TotalDay };
        var ownerRows = f.Rows.Select((value, index) => new CropTeacherManagedProductionTransition(index + 1,
            index == f.Rows.Count - 1 ? "supporting_route" : "declared_original_transition", value)).ToArray();
        var manifest = new JsonObject { ["owner_transitions"] = JsonSerializer.SerializeToNode(ownerRows, JsonOptions) };
        var assembly = typeof(CropTeacherManagedSeedOwner).Assembly;
        var artifacts = assembly.GetType("StardewAI.LiveTrainingLoop.CropSeedRuntimeArtifacts")!;
        var helper = assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherTerminalShippingSelection")!
            .GetMethod("ReadNativeRetentionControlTail", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? ReadTail(JsonObject document, IReadOnlyDictionary<string, string> pins) => helper.Invoke(null,
            [Activator.CreateInstance(artifacts, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, [pins], null)!,
                retention, document, row.AfterSnapshotPath]);
        Assert.Single(Assert.IsType<CropTeacherManagedProductionTransition[]>(ReadTail(manifest, hashes)));
        foreach (var fault in new[] { "role", "sequence", "missing" })
        {
            var changed = (JsonObject)manifest.DeepClone(); var tail = changed["owner_transitions"]!.AsArray();
            if (fault == "role") tail[^1]!["operationKind"] = "terminal_shipping";
            if (fault == "sequence") tail[^1]!["ownerSequence"] = 1;
            if (fault == "missing") tail.RemoveAt(tail.Count - 1);
            Assert.Null(ReadTail(changed, hashes));
        }
        var unpinned = new Dictionary<string, string>(hashes); unpinned.Remove(row.ExecutionReceiptPath);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ReadTail(manifest, unpinned)).InnerException);
    }

    [Fact]
    public async Task NativeLifecycleConsumesSixTileDay8Water4OriginalProductRefillApproachWater2()
    {
        var f = new LifecycleControlsFixture(combinedRefill: true); await f.Build();
        var paths = f.Rows.SelectMany(row => new[] { row.QueuePath, row.BeforeSnapshotPath, row.ExecutionReceiptPath, row.AfterSnapshotPath })
            .Concat(f.PreparationInputs.Keys).Distinct().ToArray();
        var hashes = paths.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        Assert.Empty((Array)f.Validate());
        var loaded = LifecycleLoad(f.Rows);
        var day = f.Rows.Where(row => row.TotalDay == f.SplitDay).ToArray();
        Assert.Equal(new[] { "executor.traverse_connector", "executor.wait_ticks", "executor.water_crop", "executor.move_to_tile",
            "executor.water_crop", "executor.traverse_connector", "executor.wait_ticks", "executor.sleep" },
            day.Select(row => LifecycleRead<ActionQueueEnvelope>(row.QueuePath).Items[0].OptionId));
        Assert.Equal(new[] { 4, 2 }, day.Select(row => LifecycleRead<ActionQueueEnvelope>(row.QueuePath))
            .Where(queue => queue.Items[0].OptionId == "executor.water_crop").Select(queue => queue.Items.Length));
        Assert.Equal(6, LifecycleInvoke("CountOption", LifecycleLoad(day), "executor.water_crop"));
        Assert.Equal(6, LifecycleInvoke("CountOption", loaded, "executor.plant_seed"));
        Assert.Equal(6, LifecycleInvoke("CountOption", loaded, "executor.harvest_crop"));
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.move_to_tile"));
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.wait_ticks"));
        var preparationIndex = f.Rows.FindIndex(row => row.NativeRefillPreparation is not null);
        var preparation = f.Rows[preparationIndex];
        var continuation = f.Rows[preparationIndex + 1];
        Assert.Equal(LifecycleRead<SnapshotEnvelope>(f.Rows[preparationIndex - 1].AfterSnapshotPath).StateHash,
            LifecycleRead<SnapshotEnvelope>(preparation.BeforeSnapshotPath).StateHash);
        Assert.Equal(LifecycleRead<SnapshotEnvelope>(preparation.AfterSnapshotPath).StateHash,
            LifecycleRead<SnapshotEnvelope>(continuation.BeforeSnapshotPath).StateHash);
        var actions = LifecycleList(LifecycleLoad(day.Where(row => row.TransitionKind == "crop_actions")));
        LifecycleInvoke("ValidateNativePreparationContinuations", actions, f.Contexts);
        // No later water, earlier-only water, and another commitment must not satisfy the original preparation.
        foreach (var fault in new[] { "missing", "earlier-only", "wrong-commitment" })
        {
            var rows = day.Where(row => row.TransitionKind == "crop_actions").ToList();
            if (fault == "missing") rows.Remove(continuation);
            if (fault == "earlier-only") { rows.Remove(continuation); rows.Insert(0, continuation); }
            // Exercise the continuation guard directly after native loading: this is a negative-only
            // owner substitution, not a new accepted compilation or a positive witness.
            var negative = LifecycleList(LifecycleLoad(rows));
            if (fault == "wrong-commitment")
            {
                var index = rows.IndexOf(preparation); var entry = negative[index]!;
                entry.GetType().GetProperty("Request")!.SetValue(entry, preparation with
                { NativeRefillPreparation = preparation.NativeRefillPreparation! with { CommitmentId = "another-commitment" } });
            }
            var error = Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
                LifecycleInvoke("ValidateNativePreparationContinuations", negative, f.Contexts)).InnerException);
            Assert.Contains("no later same-day watering continuation", error.Message);
        }
        foreach (var fault in new[] { "duplicate", "missing", "reordered", "candidate", "unwitnessed" })
        {
            var rows = f.Rows.ToList();
            if (fault == "duplicate") rows.Insert(preparationIndex, preparation);
            if (fault == "missing") rows.RemoveAt(preparationIndex + 1);
            if (fault == "reordered") (rows[preparationIndex], rows[preparationIndex + 1]) = (rows[preparationIndex + 1], rows[preparationIndex]);
            if (fault == "candidate") rows[preparationIndex] = preparation with { SelectedCandidateId = "unrelated" };
            if (fault == "unwitnessed") rows[preparationIndex] = preparation with { NativeRefillPreparation = null };
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(rows)).InnerException);
        }
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(native: false)).InnerException);
        for (var index = 0; index < paths.Length; index++) Assert.Equal(hashes[index], SHA256.HashData(File.ReadAllBytes(paths[index])));
    }

    [Fact]
    public async Task NativeLifecycleConsumesPreWaterInterBatchAndFinalControlsWithoutCropCredit()
    {
        var f = new LifecycleControlsFixture(); await f.Build();
        var paths = f.Rows.SelectMany(row => new[] { row.QueuePath, row.ExecutionReceiptPath }).ToArray();
        var hashes = paths.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        Assert.Empty((Array)f.Validate());
        var loaded = LifecycleLoad(f.Rows);
        var controls = f.Rows.Count(row => row.SelectedCandidateId == StardewAI.Core.Training.NativeControlSettleRecovery.CandidateId);
        Assert.Equal(4, controls);
        Assert.Equal(2, LifecycleInvoke("CountOption", loaded, "executor.plant_seed"));
        Assert.Equal(2, LifecycleInvoke("CountOption", loaded, "executor.harvest_crop"));
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.wait_ticks"));
        for (var i = 0; i < paths.Length; i++) Assert.Equal(hashes[i], SHA256.HashData(File.ReadAllBytes(paths[i])));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(native: false)).InnerException);
        foreach (var fault in new[] { "duplicate", "missing", "reordered", "misbinding" })
        {
            var rows = f.Rows.ToList(); var index = rows.FindIndex(row => row.SelectedCandidateId == StardewAI.Core.Training.NativeControlSettleRecovery.CandidateId);
            if (fault == "duplicate") rows.Insert(index, rows[index]);
            if (fault == "missing") rows.RemoveAt(index + 1);
            if (fault == "reordered") (rows[index], rows[index + 1]) = (rows[index + 1], rows[index]);
            if (fault == "misbinding") rows[index] = rows[index] with { SelectedCandidateId = "unrelated" };
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => f.Validate(rows)).InnerException);
        }
    }

    [Fact]
    public async Task NativeLifecycleReopensOriginalProductRefillPreparationWithoutProductionCredit()
    {
        using var f = new NativeRefillProductFixture(); await f.Build();
        var approach = f.Trace.Approach; var shared = approach.Watering.Shared;
        var preparation = f.OpenPreparation(selected: false, native: true);
        var row = f.Transition(preparation, StardewAI.Contracts.Training.PolicyTrajectoryVersionPins.ProductExecutor) with
        {
            NativeRefillPreparation = new(shared.Inputs.Plan.RequestPath, shared.Source.LedgerPath,
                shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill-approach.original", approach.ModelPath, "native_resource_neutral")
        };
        var hashes = new[] { row.QueuePath, row.ExecutionReceiptPath }.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        var loaded = LifecycleLoad(new[] { row });
        Assert.True((bool)loaded.GetValue(0)!.GetType().GetProperty("NativeNonProduction")!.GetValue(loaded.GetValue(0))!);
        Assert.Equal(0, LifecycleInvoke("CountOption", loaded, "executor.move_to_tile"));
        Assert.Equal(hashes[0], SHA256.HashData(File.ReadAllBytes(row.QueuePath)));
        Assert.Equal(hashes[1], SHA256.HashData(File.ReadAllBytes(row.ExecutionReceiptPath)));
        foreach (var bad in new[] { row with { SelectedCandidateId = "unrelated" }, row with
                 { NativeRefillPreparation = row.NativeRefillPreparation with { CommitmentId = "unrelated" } } })
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => LifecycleLoad(new[] { bad })).InnerException);
        Assert.DoesNotContain("native_refill_preparation", System.Text.Json.JsonSerializer.Serialize(row with { NativeRefillPreparation = null }, JsonOptions));
    }
}
