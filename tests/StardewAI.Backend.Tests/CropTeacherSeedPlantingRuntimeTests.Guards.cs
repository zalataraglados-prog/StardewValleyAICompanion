using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalControlCompilerUsesTheSamePrefixOwnerAndMechanicalBatch(bool managedScopes)
    {
        using var f = new RuntimeFixture();
        var runtime = await f.BuildControl(managedScopes);
        Assert.Equal(8, f.Queue.Items.Length);
        Assert.Equal("pending", f.Queue.Status);
        runtime.ValidateDispatchQueue(f.Files.QueuePath);
        var receipt = await f.Execute(runtime);
        Assert.True(receipt["success"]!.GetValue<bool>(), receipt.ToJsonString());
        await runtime.CompleteAsync(f.Files.QueuePath, receipt["execution_path"]!.GetValue<string>(),
            receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId);
        Assert.Equal(8, f.NativeRequests);
        Assert.Equal(1, f.Handler.Commits);
        Assert.Equal(1, f.Handler.Settlements);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("receipt")]
    [InlineData("attempt_budget")]
    [InlineData("executor_override")]
    public async Task UnsupportedControlProfileStopsBeforeOriginalMarkerMutation(string fault)
    {
        using var f = new RuntimeFixture();
        if (fault == "profile") f.Options.TargetExecutionMode = "companion_coop";
        if (fault == "receipt") f.Options.EmitQueueExecutionReceipt = false;
        if (fault == "attempt_budget") f.Options.MaxQueueItemAttempts = 24;
        if (fault == "executor_override") f.Options.ExecutorOptionId = "executor.plant_seed";
        await Assert.ThrowsAsync<InvalidDataException>(() => f.BuildControl());
        Assert.Equal(0, f.Handler.Commits);
        Assert.Equal(0, f.NativeRequests);
    }

    [Fact]
    public async Task UnverifiedNativeEffectIsRetainedButStopsBeforeAnySecondDispatchOrSettlement()
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare(consumed: 1, change: snapshot =>
        {
            var actor = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            actor["money"]!["value"] = actor["money"]!["value"]!.GetValue<int>() + 1;
            snapshot.State["player"] = System.Text.Json.JsonSerializer.SerializeToElement(actor, JsonOptions);
        });
        var receipt = await f.Execute(runtime);
        var step = Assert.Single(receipt["step_results"]!.AsArray());
        Assert.Equal(1, f.NativeRequests);
        Assert.Equal("verified", step!["owned_seed_native_verification_status"]!.GetValue<string>());
        Assert.Equal("owned_seed_prefix_unverified", step["primitive_verification_status"]!.GetValue<string>());
        Assert.False(step["selected_queue_candidate_completed"]!.GetValue<bool>());
        Assert.True(File.Exists(step["execution_request_journal_path"]!.GetValue<string>()));
        Assert.False(receipt["success"]!.GetValue<bool>());
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.CompleteAsync(f.Files.QueuePath,
            receipt["execution_path"]!.GetValue<string>(), receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId));
        Assert.Equal(0, f.Handler.Settlements);
    }

    [Theory]
    [InlineData("tick")]
    [InlineData("mode")]
    [InlineData("fresh")]
    [InlineData("status")]
    public async Task ZeroDispatchDiagnosticCannotBeForgedIntoANativeOutcome(string fault)
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare();
        var receipt = await f.Execute(runtime, attachOwner: false);
        if (fault == "tick") receipt["after_game_tick"] = receipt["after_game_tick"]!.GetValue<long>() + 1;
        if (fault == "mode") receipt["queue_execution_mode"] = "invented";
        if (fault == "fresh") receipt["after_snapshot_fresh"] = true;
        if (fault == "status") receipt["status"] = "applied";
        await File.WriteAllTextAsync(receipt["execution_path"]!.GetValue<string>(), receipt.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.CompleteAsync(f.Files.QueuePath,
            receipt["execution_path"]!.GetValue<string>(), receipt["after_snapshot_path"]!.GetValue<string>(), f.Transition.SelectedCandidateId));
        Assert.Equal(0, f.NativeRequests);
        Assert.Equal(0, f.Handler.Settlements);
    }

    [Theory]
    [InlineData("terminal_tick")]
    [InlineData("step")]
    [InlineData("after_file")]
    public async Task CompletionIsBoundToTheObservedReceiptAndArtifactsNotOnlySuccessCount(string fault)
    {
        using var f = new RuntimeFixture();
        var runtime = await f.Prepare();
        await f.Observe(runtime);
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(f.Transition.ExecutionReceiptPath))!;
        if (fault == "terminal_tick") receipt["after_game_tick"] = receipt["after_game_tick"]!.GetValue<long>() + 1;
        if (fault == "step") receipt["step_results"]![0]!["changed_facts"] = new JsonArray(new JsonObject { ["observed_replacement"] = true });
        if (fault == "after_file")
        {
            var path = receipt["step_results"]![0]!["after_snapshot_path"]!.GetValue<string>();
            await File.AppendAllTextAsync(path, "\n");
        }
        else await File.WriteAllTextAsync(f.Transition.ExecutionReceiptPath, receipt.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.CompleteAsync(f.Transition));
        Assert.Equal(0, f.Handler.Settlements);
    }
}
