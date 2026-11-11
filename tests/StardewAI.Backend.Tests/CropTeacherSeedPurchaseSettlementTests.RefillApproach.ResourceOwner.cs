using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedRefillResourcePreparationRebindsOriginalLedgerWithoutCropCredit(bool observe)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true,
            observeNativeOutputs: observe);
        await f.Build();
        var shared = f.Approach.Watering.Shared;
        var preparation = CropTeacherWateringMaintenance.Preparation.OpenRefillApproachWithSelectedResourceEffects(
            shared.Inputs.Plan.RequestPath, shared.Source.SourcePath, shared.Source.LedgerPath,
            shared.Inputs.Plan.CoveredCommitmentIds, Id, "refill.selected-resource", f.Approach.ModelPath, f.Approach.QueuePath);
        Assert.True(preparation.UsesSelectedRefillResourceEffects);
        var result = preparation.VerifyTransition(NeutralTransition(f, preparation));
        Assert.True(result.RefillApproachVerified);
        Assert.True(result.RefillResourceEffectsVerified);
        Assert.Equal(0, result.AppliedTileCount);
        Assert.Empty(result.AppliedTiles);
        Assert.False(result.CompleteBatch);
        Assert.False(result.NativeCropLifecycleVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var trace = typeof(CropTeacherWateringMaintenance).GetField("refillTrace", privateInstance)!.GetValue(result)!;
        var rebind = typeof(CropTeacherWateringMaintenance).GetField("rebind", privateInstance)!.GetValue(result)!;
        Assert.Same(trace.GetType().GetProperty("VerifiedAfterSnapshot", privateInstance)!.GetValue(trace),
            rebind.GetType().GetField("after", privateInstance)!.GetValue(rebind));
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        var reply = new ReservationPortfolioLedgerService().Commit(shared.Source.Ledger, after, result.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(shared.Source.Ledger, result.VerifyRebindReply(reply));
        result.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelectedRefillResourceOwnerRefusesUnsupportedRepairOrWrongPhaseBeforeDispatch(bool repair)
    {
        using var f = new RefillApproachFixture(repair);
        await f.Compile();
        await using var owner = await f.Watering.Shared.Files.Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => AdmitSelectedResourceRefill(owner, f, isRefill: repair));
        Assert.Null(owner.Pending);
        Assert.False(File.Exists(Path.Combine(f.Watering.Shared.Files.Native, "maintenance-native-claim.json")));
    }

    [Theory]
    [InlineData("live")]
    [InlineData("native_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    [InlineData("corrupt_resource_flag")]
    [InlineData("legacy_forged_resource_flag")]
    public async Task SelectedRefillResourceOwnerKeepsOriginalWireRecoveryAndZeroCropProgress(string mode)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        await f.Approach.Compile();
        var shared = f.Approach.Watering.Shared;
        var loseWrite = true;
        await using var owner = await shared.Files.Open(writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            var target = mode switch { "native_ack_lost" => "maintenance-native-result.json",
                "marker_reply_lost" => "marker-reply.json", "acceptance_ack_lost" => "operation-000001-accepted.json", _ => "" };
            if (loseWrite && Path.GetFileName(path) == target)
            { loseWrite = false; throw new IOException("Declared selected-resource acknowledgement lost after durable write."); }
        });
        var legacy = mode == "legacy_forged_resource_flag";
        var operation = legacy ? await AdmitNeutralRefill(owner, f.Approach) : await AdmitSelectedResourceRefill(owner, f.Approach);
        var admitted = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-intent.json")))!;
        Assert.Equal(legacy ? "refill_approach" : "refill_approach_resources", admitted["maintenance_control_kind"]!.GetValue<string>());
        await f.Build(retainCompilation: true);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!;
        using var transport = new OriginalWateringTransport(shared.Runtime, f.Approach.Watering.Before, receipt, fullExecutionResult: true);
        using var client = new HttpClient(transport);
        Task Execute() => InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync", client, client,
            shared.Runtime.LiveOptions, 1, owner, operation);
        if (mode is "live" or "corrupt_resource_flag" or "legacy_forged_resource_flag") await Execute();
        else await Assert.ThrowsAsync<IOException>(Execute);
        Assert.Equal(1, transport.NativeRequests);
        await owner.DisposeAsync();
        if (mode is "corrupt_resource_flag" or "legacy_forged_resource_flag")
        {
            var path = Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json");
            var accepted = JsonNode.Parse(File.ReadAllText(path))!;
            accepted["native_refill_resource_effects_verified"] = legacy;
            Fixture.Save(path, accepted);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => shared.Files.Open());
            Assert.Contains("watering prefix acceptance changed", error.Message);
            return;
        }
        await using var reopened = await shared.Files.Open();
        if (mode == "native_ack_lost")
            await reopened.RunWateringMaintenanceAsync(reopened.Pending!, shared.Runtime.Client,
                (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync",
                    shared.Runtime.Client, shared.Runtime.LiveOptions, diagnostic, request), shared.Files.Post);
        else if (mode == "marker_reply_lost")
            await reopened.RecoverPendingAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        else await reopened.RestoreHeadAsync(shared.Runtime.Client, shared.Files.Recovery, shared.Files.Post);
        Assert.Equal(1, transport.NativeRequests);
        Assert.Equal(1, shared.Runtime.Handler.Commits);
        Assert.Null(reopened.Pending);
        Assert.False(reopened.HeadRecoveryRequired);
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(result["native_refill_resource_effects_verified"]!.GetValue<bool>());
        Assert.True(result["native_refill_approach_verified"]!.GetValue<bool>());
        Assert.False(result["native_watering_prefix_verified"]!.GetValue<bool>());
        Assert.Equal(0, result["applied_tile_count"]!.GetValue<int>());
        Assert.False(result["complete_batch"]!.GetValue<bool>());
        Assert.False(result["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(result["formal_product_training_authorized"]!.GetValue<bool>());
    }

    private static Task<CropTeacherManagedSeedOperation> AdmitSelectedResourceRefill(CropTeacherManagedSeedOwner owner,
        RefillApproachFixture f, bool isRefill = true) => owner.CompileAndAdmitOriginalWateringAsync(() =>
        Task.FromResult(new CropTeacherOriginalWateringCompilation(f.Watering.Step, f.ModelPath, f.QueuePath,
            f.Watering.Shared.ReceiptPath, f.Watering.Shared.AfterPath, f.Watering.Shared.Files.Native)
            { IsRefillApproach = isRefill, UseSelectedRefillResourceEffects = true }));

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void SelectedRefillResourceToolEvidenceMissingIsRejectedBeforeNativeClaim(bool outputs, bool tools, bool rejects)
    {
        using var f = new RefillApproachFixture(repair: false);
        var options = f.Watering.Shared.Runtime.LiveOptions;
        options.ObserveNativeOutputs = outputs; options.ObserveNativeClearanceToolInputs = tools;
        var record = new JsonObject { ["maintenance_control_kind"] = "refill_approach_resources" };
        var queue = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["option_id"] = "executor.clear_obstacle" }) };
        void Check() => InvokeLive<object?>("RequireSelectedRefillToolObservation", options, record, queue);
        if (rejects)
            Assert.IsType<InvalidDataException>(Assert.Throws<System.Reflection.TargetInvocationException>(Check).InnerException);
        else Check();
        queue["items"]![0]!["option_id"] = "executor.move_to_tile";
        Check(); // Neutral moves never acquire an optional XML/tool observation requirement.
        record["maintenance_control_kind"] = "refill_approach";
        queue["items"]![0]!["option_id"] = "executor.clear_obstacle";
        Check(); // The new mode does not change historical protocol validation.
    }
}
