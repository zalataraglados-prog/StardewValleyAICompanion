using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedRefillResourceSelectionRetainsExactOwnerIdentityAcrossReopen(bool selected)
    {
        using var f = new OwnedWateringFixture();
        var files = f.Shared.Files;
        Assert.False(files.Options.UseSelectedRefillResourceEffects);
        var options = files.Options with { UseSelectedRefillResourceEffects = selected };
        await using (var owner = await files.Open(options: options))
            Assert.Null(owner.Pending);
        var path = Path.Combine(files.OwnerRoot, "owner-intent.json");
        var retained = File.ReadAllText(path);
        var header = JsonNode.Parse(retained)!;
        if (selected) Assert.True(header["selected_refill_resource_effects"]!.GetValue<bool>());
        else Assert.False(header.AsObject().ContainsKey("selected_refill_resource_effects"));
        Assert.False(header["execution_authorized"]!.GetValue<bool>());
        Assert.False(header["formal_product_training_authorized"]!.GetValue<bool>());
        await using (var reopened = await files.Open(options: options))
            Assert.Null(reopened.Pending);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => files.Open(options:
            options with { UseSelectedRefillResourceEffects = !selected }));
        Assert.Contains("does not match its retained original identity", error.Message);
        Assert.Equal(retained, File.ReadAllText(path));
        // A rejected reopen must also release its lease, without admission or dispatch.
        await using var unchanged = await files.Open(options: options);
        AssertOwnerSelectionHasNoAdmission(unchanged, f.Shared);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedRefillResourceSelectionPrivatePreparationUsesOnlyRetainedExplicitSetting(bool selected)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        var shared = f.Approach.Watering.Shared;
        var options = shared.Files.Options with { UseSelectedRefillResourceEffects = selected };
        await using var owner = await shared.Files.Open(options: options);
        // No Build(): the actual private preparation must compile and choose the phase.
        Assert.False(shared.Runtime.LiveOptions.ObserveNativeOutputs);
        Assert.False(shared.Runtime.LiveOptions.ObserveNativeInventoryPayload);
        Assert.False(shared.Runtime.LiveOptions.ObserveNativeClearanceToolInputs);
        var operation = await PrepareOwnerSelectedRefill(f, owner);
        Assert.Same(operation, owner.Pending);
        Assert.True(operation.IsWateringMaintenance);
        var admitted = ReadOwnerSelectionIntent(shared);
        Assert.Equal(selected ? "refill_approach_resources" : "refill_approach",
            admitted["maintenance_control_kind"]!.GetValue<string>());
        Assert.Equal("original_loop.v1", admitted["maintenance_execution_protocol"]!.GetValue<string>());
        var header = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "owner-intent.json")))!;
        if (selected) Assert.True(header["selected_refill_resource_effects"]!.GetValue<bool>());
        else Assert.False(header.AsObject().ContainsKey("selected_refill_resource_effects"));
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "maintenance-native-claim.json")));
        Assert.Equal(0, shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OwnedRefillResourceSelectionEnabledOwnerLeavesOrdinaryWateringOrdinary()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open(options:
            f.Shared.Files.Options with { UseSelectedRefillResourceEffects = true });
        var operation = await f.Prepare(owner);
        Assert.Same(operation, owner.Pending);
        Assert.True(operation.IsWateringMaintenance);
        Assert.False(ReadOwnerSelectionIntent(f.Shared).ContainsKey("maintenance_control_kind"));
        var queue = JsonNode.Parse(File.ReadAllText(f.QueuePath))!;
        Assert.NotEmpty(queue["items"]!.AsArray());
        Assert.All(queue["items"]!.AsArray(), row =>
            Assert.Equal("executor.water_crop", row!["option_id"]!.GetValue<string>()));
        Assert.False(File.Exists(Path.Combine(f.Shared.Files.Native, "maintenance-native-claim.json")));
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task OwnedRefillResourceSelectionInvalidObservationProfileRejectsBeforeAdmission()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: true);
        var shared = f.Approach.Watering.Shared;
        shared.Runtime.LiveOptions.ObserveNativeOutputs = true;
        shared.Runtime.LiveOptions.SkipTraining = false;
        await using var owner = await shared.Files.Open(options:
            shared.Files.Options with { UseSelectedRefillResourceEffects = true });
        var error = await Assert.ThrowsAsync<ArgumentException>(() => PrepareOwnerSelectedRefill(f, owner));
        Assert.Contains("--observe-native-outputs requires --skip-training", error.Message);
        AssertOwnerSelectionHasNoAdmission(owner, shared);
    }

    [Fact]
    public async Task OwnedRefillResourceSelectionUnsupportedGrassRepairRejectsBeforeAdmission()
    {
        using var f = new RefillApproachFixture(repair: true);
        var shared = f.Watering.Shared;
        var live = shared.Runtime.LiveOptions;
        live.ObserveNativeOutputs = live.ObserveNativeClearanceToolInputs = live.ObserveNativeOutputSnapshotAnchor = true;
        live.ExecutionSnapshotProfile = "full";
        live.SkipTraining = true;
        await using var owner = await shared.Files.Open(options:
            shared.Files.Options with { UseSelectedRefillResourceEffects = true });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
                shared.Runtime.Client, live, 1, f.Watering.Step, shared.Inputs.Plan, shared.Inputs.Request,
                owner, shared.Files.Native));
        Assert.Contains("no supported selected-resource contract before dispatch", error.Message);
        AssertOwnerSelectionHasNoAdmission(owner, shared);
    }

    [Fact]
    public async Task OwnedRefillResourceSelectionOriginalPreparationRecoversDurableNativeAcknowledgementOnce()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        var shared = f.Approach.Watering.Shared;
        var options = shared.Files.Options with { UseSelectedRefillResourceEffects = true };
        var loseWrite = true;
        await using var owner = await shared.Files.Open(options: options, writer: async (path, json) =>
        {
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (loseWrite && Path.GetFileName(path) == "maintenance-native-result.json")
            {
                loseWrite = false;
                throw new IOException("Declared selected-owner native acknowledgement lost after durable write.");
            }
        });
        var operation = await PrepareOwnerSelectedRefill(f, owner);
        Assert.Equal("refill_approach_resources", ReadOwnerSelectionIntent(shared)["maintenance_control_kind"]!.GetValue<string>());
        await f.Build(retainCompilation: true);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(f.ReceiptPath), JsonOptions)!;
        using var transport = new OriginalWateringTransport(shared.Runtime, f.Approach.Watering.Before, receipt, fullExecutionResult: true);
        using var client = new HttpClient(transport);
        var error = await Assert.ThrowsAsync<IOException>(() => InvokeLive<Task>("ExecuteCropTeacherOwnedWateringAsync",
            client, client, shared.Runtime.LiveOptions, 1, owner, operation));
        Assert.Contains("acknowledgement lost after durable write", error.Message);
        Assert.False(loseWrite);
        Assert.Equal(1, transport.NativeRequests);
        Assert.Equal(0, shared.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json")));
        await owner.DisposeAsync();
        await using var reopened = await shared.Files.Open(options: options);
        Assert.NotNull(reopened.Pending);
        await reopened.RunWateringMaintenanceAsync(reopened.Pending!, shared.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync",
                shared.Runtime.Client, shared.Runtime.LiveOptions, diagnostic, request), shared.Files.Post);
        Assert.Equal(1, transport.NativeRequests);
        Assert.Equal(1, shared.Runtime.Handler.Commits);
        Assert.Null(reopened.Pending);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(shared.AfterPath, reopened.CurrentSnapshotPath);
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-accepted.json")))!;
        Assert.True(accepted["native_refill_resource_effects_verified"]!.GetValue<bool>());
        Assert.True(accepted["native_refill_approach_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_watering_prefix_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["applied_tile_count"]!.GetValue<int>());
        Assert.Empty(accepted["applied_tiles"]!.AsArray());
        Assert.False(accepted["complete_batch"]!.GetValue<bool>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.False(accepted["formal_product_training_authorized"]!.GetValue<bool>());
    }

    private static Task<CropTeacherManagedSeedOperation> PrepareOwnerSelectedRefill(RefillTraceFixture f,
        CropTeacherManagedSeedOwner owner)
    {
        var shared = f.Approach.Watering.Shared;
        return InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            shared.Runtime.Client, shared.Runtime.LiveOptions, 1, f.Approach.Watering.Step,
            shared.Inputs.Plan, shared.Inputs.Request, owner, shared.Files.Native);
    }

    private static JsonObject ReadOwnerSelectionIntent(OwnedRouteFixture shared) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-intent.json")))!.AsObject();

    private static void AssertOwnerSelectionHasNoAdmission(CropTeacherManagedSeedOwner owner, OwnedRouteFixture shared)
    {
        Assert.Null(owner.Pending);
        Assert.False(File.Exists(Path.Combine(shared.Files.OwnerRoot, "operation-000001-intent.json")));
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "maintenance-native-claim.json")));
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "maintenance-native-result.json")));
        Assert.Equal(0, shared.Runtime.Handler.Commits);
        Assert.Equal(0, shared.Runtime.Handler.Settlements);
    }
}
