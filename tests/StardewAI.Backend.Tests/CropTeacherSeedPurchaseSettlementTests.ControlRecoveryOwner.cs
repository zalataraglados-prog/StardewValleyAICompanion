using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ControlRecoveryOwnerUsesOriginalNativeLoopWithoutGenericFallbackOrCropProgress()
    {
        using var f = new OwnedRouteFixture(prepareSource: SetOwnedSafeDialogue);
        await using var owner = await f.Files.Open();
        var operation = (await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step))!;
        Assert.True(operation.IsControlRecovery); Assert.False(operation.IsSupportingRoute);
        InvokeLive<object>("RequireManagedCropDispatch", owner, operation);
        var result = BuildOwnedControlResult(f, owner);
        using var transport = new OriginalRouteTransport(f, result);
        using var client = new HttpClient(transport);
        await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", client, client, f.Runtime.LiveOptions,
            1, owner, operation, OwnedControlQueue(f),
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("Generic fallback cannot dispatch owned control.")));
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Null(owner.Pending); Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
        var accepted = OwnedControlAccepted(f, operation);
        Assert.True(accepted["native_control_recovery_verified"]!.GetValue<bool>());
        Assert.Equal("safe_dialogue_recovery", accepted["control_kind"]!.GetValue<string>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.False(accepted["teacher_preference_label_eligible"]!.GetValue<bool>());
        Assert.Null(accepted["route_target_reached"]); Assert.Null(accepted["applied_tile_count"]);
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.False(reopened.HeadRecoveryRequired); Assert.Equal(f.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("native_ack_lost")]
    [InlineData("native_result_ack_lost")]
    [InlineData("marker_reply_lost")]
    [InlineData("acceptance_ack_lost")]
    public async Task ControlRecoveryOwnerRestartsWithOneOriginalNativeControlAndMarker(string mode)
    {
        using var f = new OwnedRouteFixture(prepareSource: SetOwnedSafeDialogue);
        var loseWrite = true;
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            if (mode == "marker_reply_lost" && loseWrite && Path.GetFileName(path) == "marker-reply.json")
            { loseWrite = false; throw new IOException("Control marker committed before acknowledgement."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
            if (loseWrite && (mode == "native_result_ack_lost" && Path.GetFileName(path) == "control-native-result.json" ||
                mode == "acceptance_ack_lost" && Path.GetFileName(path) == "operation-000001-accepted.json"))
            { loseWrite = false; throw new IOException("Durable control record acknowledgement lost."); }
        });
        var operation = (await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step, 2));
        var calls = 0;
        Task<JsonObject> Native()
        {
            calls++;
            var (after, receipt) = BuildOwnedControlResult(f, owner);
            Fixture.Save(f.AfterPath, after); Fixture.Save(f.ReceiptPath, receipt);
            f.Runtime.Store.Snapshots[after.StateHash] = after;
            if (mode == "native_ack_lost") throw new IOException("Control output retained before callback acknowledgement.");
            return Task.FromResult(new JsonObject { ["execution_path"] = f.ReceiptPath, ["after_snapshot_path"] = f.AfterPath });
        }
        Task Rebind(CropTeacherManagedSeedOwner current, CropTeacherManagedSeedOperation pending) => current.RunControlRecoveryAsync(pending,
            f.Runtime.Client, (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync",
                f.Runtime.Client, f.Runtime.LiveOptions, diagnostic, request), f.Files.Post);
        if (mode is "native_ack_lost" or "native_result_ack_lost")
            await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalControlAsync(operation, f.Runtime.Client, Native));
        else
        {
            await owner.ExecuteOriginalControlAsync(operation, f.Runtime.Client, Native);
            await Assert.ThrowsAsync<IOException>(() => Rebind(owner, operation));
        }
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        if (mode == "acceptance_ack_lost") await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else if (mode == "marker_reply_lost") await reopened.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        else await Rebind(reopened, reopened.Pending!);
        Assert.Null(reopened.Pending); Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(1, calls); Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Equal(mode == "marker_reply_lost" ? 1 : 0, f.Runtime.Handler.Lookups);
    }

    [Fact]
    public async Task ControlRecoveryOwnerDoesNotRepeatAnUnknownNativeInvocation()
    {
        using var f = new OwnedRouteFixture(prepareSource: SetOwnedSafeDialogue);
        await using var owner = await f.Files.Open();
        var operation = (await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step))!;
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalControlAsync(operation, f.Runtime.Client, () =>
        { calls++; throw new IOException("Unknown control result."); }));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ExecuteOriginalControlAsync(reopened.Pending!, f.Runtime.Client, () =>
        { calls++; throw new InvalidOperationException("No repeat."); }));
        await Assert.ThrowsAnyAsync<IOException>(() => reopened.RunControlRecoveryAsync(reopened.Pending!, f.Runtime.Client,
            (diagnostic, request) => throw new InvalidOperationException("Unverified control cannot commit."), f.Files.Post));
        Assert.Equal(1, calls); Assert.Equal(0, f.Runtime.Handler.Commits); Assert.NotNull(reopened.Pending);
    }

    private static string OwnedControlQueue(OwnedRouteFixture f) =>
        Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "crop-teacher-dialogue-recovery-0001-compiled-queue.json");

    private static JsonNode OwnedControlAccepted(OwnedRouteFixture f, CropTeacherManagedSeedOperation operation) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, $"operation-{operation.Sequence:D6}-accepted.json")))!;

    private static (SnapshotEnvelope After, QueueExecutionReceiptEnvelope Receipt) BuildOwnedControlResult(OwnedRouteFixture f,
        CropTeacherManagedSeedOwner owner)
    {
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        ChangeState(after, "menus", menu => menu["active_menu"]!["value"] = JsonSerializer.SerializeToNode(new { is_open = false, type = "none" }));
        after.GameTick++;
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(OwnedControlQueue(f)), JsonOptions)!;
        var candidate = queue.Items.Single().NormalizedCommand.Parameters.Single(row => row.Name == "precondition" &&
            row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
        return (after, MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
            f.Files.Options.RunId, candidate, "close_menu"));
    }

    private static void SetOwnedSafeDialogue(Fixture source) => source.Before.State["menus"] = JsonSerializer.SerializeToElement(new
    {
        active_menu = new { status = "available", value = new
        {
            is_open = true, type = "DialogueBox", is_sleep_prompt = false, event_up = false,
            dialogue_is_question = false, dialogue_response_count = 0, dialogue_transitioning = false,
            dialogue_character_present = false, dialogue_speaker_name = "", last_question_key = ""
        } },
        sleep_prompt_context = new { status = "available", value = new { prompt_open = false } }
    });
}
