using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ControlRecoveryProofSharedClassifierPreservesExactOriginalRules()
    {
        var snapshot = new SnapshotEnvelope { State = new() { ["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = SafeControlMenu() }
        }) } };
        void Check(bool expected)
        {
            var json = JsonSerializer.SerializeToNode(snapshot, JsonOptions)!.AsObject();
            Assert.Equal(expected, SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(snapshot));
            Assert.Equal(expected, SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(json));
            Assert.Equal(expected, CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(json));
        }
        Check(true);
        ChangeState(snapshot, "menus", node => node["active_menu"]!["value"]!["dialogue_transitioning"] = true);
        Check(true); // HasValue, not false: preserve the existing classifier.
        foreach (var key in new[] { "Sleep", "SleepTent" })
        {
            ChangeState(snapshot, "menus", node => node["active_menu"]!["value"]!["last_question_key"] = key);
            Check(false);
        }
        ChangeState(snapshot, "menus", node => node["active_menu"]!["value"]!["last_question_key"] = null);
        ChangeState(snapshot, "menus", node => node["active_menu"]!["status"] = "derived");
        Check(false);
        ChangeState(snapshot, "menus", node => node["active_menu"]!["status"] = "available");
        ChangeState(snapshot, "menus", node => node["active_menu"]!["value"]!.AsObject().Remove("dialogue_speaker_name"));
        Check(false);
    }

    [Fact]
    public async Task ControlRecoveryProofOriginalCloseRebindAndRetainedEvidence()
    {
        using var f = ControlProofFixture();
        var (model, queue) = await BuildControlProof(f);
        var preparation = OpenControlProof(f, model, queue);
        Assert.Equal(5, preparation.InputHashes.Count);
        var transition = EmitControlProof(f, preparation, queue);
        var outcome = preparation.VerifyTransition(transition);
        Assert.Equal("safe_dialogue_recovery", outcome.Kind);
        Assert.False(outcome.NativeCropLifecycleVerified);
        Assert.False(outcome.FormalProductTrainingAuthorized);
        Assert.Equal(7, outcome.InputHashes.Count);
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        var reply = new ReservationPortfolioLedgerService().Commit(f.Source.Ledger, after, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        Assert.Equal(reply.Ledger!.Revision, outcome.VerifyRebindReply(reply).Revision);
        File.AppendAllText(model, " ");
        Assert.Throws<InvalidDataException>(preparation.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => outcome.RebindRequest);
    }

    [Theory]
    [InlineData("unsafe-source")]
    [InlineData("model")]
    [InlineData("actor")]
    [InlineData("queue")]
    [InlineData("kind")]
    public async Task ControlRecoveryProofRejectsChangedOriginalPreparation(string fault)
    {
        using var f = ControlProofFixture();
        var (modelPath, queuePath) = await BuildControlProof(f);
        if (fault == "unsafe-source")
        {
            var before = f.Source.Before;
            ChangeState(before, "menus", node => node["active_menu"]!["value"]!["dialogue_is_question"] = true);
            before.StateHash = SnapshotHash.ComputeStateHash(before.State);
            Fixture.Save(f.Source.SourcePath, before);
        }
        if (fault is "model" or "actor")
        {
            var plan = JsonNode.Parse(File.ReadAllText(modelPath))!;
            if (fault == "model") plan["source_model"] = "other";
            else plan["actor"]!["actor_id"] = "other";
            Fixture.Save(modelPath, plan);
        }
        if (fault == "queue")
        {
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(queuePath), JsonOptions)!;
            queue.Items[0].NormalizedCommand.Steps[0].StepType = "wait_ticks";
            Fixture.Save(queuePath, queue);
        }
        Assert.Throws<InvalidDataException>(() => OpenControlProof(f, modelPath, queuePath,
            fault == "kind" ? "unknown_control" : "safe_dialogue_recovery"));
    }

    [Theory]
    [InlineData("resources")]
    [InlineData("position")]
    [InlineData("identity")]
    [InlineData("day")]
    [InlineData("clock")]
    [InlineData("unclosed")]
    [InlineData("unreadable-menu")]
    [InlineData("candidate")]
    public async Task ControlRecoveryProofRejectsUnprovenNativeOutcome(string fault)
    {
        using var f = ControlProofFixture();
        var (model, queue) = await BuildControlProof(f);
        var preparation = OpenControlProof(f, model, queue);
        var transition = EmitControlProof(f, preparation, queue, fault);
        Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(transition));
    }

    [Fact]
    public async Task ControlRecoveryProofRejectsRetainedReceiptEdit()
    {
        using var f = ControlProofFixture();
        var (model, queue) = await BuildControlProof(f);
        var preparation = OpenControlProof(f, model, queue);
        var transition = EmitControlProof(f, preparation, queue);
        var outcome = preparation.VerifyTransition(transition);
        File.AppendAllText(f.ReceiptPath, " ");
        Assert.Throws<InvalidDataException>(outcome.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => outcome.RebindRequest);
    }

    private static JsonObject SafeControlMenu() => new()
    {
        ["is_open"] = true, ["type"] = "DialogueBox", ["last_question_key"] = null,
        ["is_sleep_prompt"] = false, ["event_up"] = false, ["dialogue_is_question"] = false,
        ["dialogue_response_count"] = 0, ["dialogue_transitioning"] = false,
        ["dialogue_character_present"] = false, ["dialogue_speaker_name"] = null
    };

    private static OwnedRouteFixture ControlProofFixture() => new(prepareSource: f =>
        f.Before.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = SafeControlMenu() }
        }));

    private static async Task<(string Model, string Queue)> BuildControlProof(OwnedRouteFixture f)
    {
        await InvokeLive<Task>("BuildCropTeacherDialogueRecoveryQueueAsync", f.Runtime.Client, f.Runtime.LiveOptions, 1,
            f.Source.SourcePath, JsonSerializer.SerializeToNode(f.Source.Before, JsonOptions)!.AsObject(), f.Source.Before.StateHash);
        var prefix = Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "crop-teacher-dialogue-recovery-0001");
        return (prefix + "-model-plan.json", prefix + "-compiled-queue.json");
    }

    private static CropTeacherControlRecovery.Preparation OpenControlProof(OwnedRouteFixture f, string model, string queue,
        string kind = "safe_dialogue_recovery") => CropTeacherControlRecovery.Preparation.Open(f.Inputs.Plan.RequestPath,
            f.Source.SourcePath, f.Source.LedgerPath, new[] { Id }, "control.proof.step", kind, model, queue);

    private static MoneyRevenueCropNativeLifecycleTransitionRequest EmitControlProof(OwnedRouteFixture f,
        CropTeacherControlRecovery.Preparation preparation, string queuePath, string fault = "")
    {
        var before = f.Source.Before;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        after.GameTick++;
        ChangeState(after, "menus", node => node["active_menu"]!["value"]!["is_open"] = fault == "unclosed");
        if (fault == "resources") ChangeState(after, "player", node => node["money"]!["value"] = 1);
        if (fault == "position") ChangeState(after, "player", node => node["tile_x"]!["value"] = 4);
        if (fault == "identity") after.PlayerId += ":other";
        if (fault == "day") ChangeState(after, "time", node =>
            node["total_days"]!["value"] = preparation.TotalDay + 1);
        if (fault == "clock") ChangeState(after, "time", node => node["time"]!["value"] = 500);
        if (fault == "unreadable-menu") ChangeState(after, "menus", node => node["active_menu"]!["status"] = "unavailable");
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(queuePath), JsonOptions)!;
        var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
            f.Files.Options.RunId, preparation.CandidateId, "close_menu");
        receipt.StepResults[0].AfterSnapshotPath = f.AfterPath;
        Fixture.Save(f.AfterPath, after);
        Fixture.Save(f.ReceiptPath, receipt);
        return new("control_recovery", preparation.TotalDay, queuePath, f.Source.SourcePath,
            f.ReceiptPath, f.AfterPath, receipt.RunId, f.Files.Options.ExecutorVersion,
            fault == "candidate" ? "other" : preparation.CandidateId);
    }
}
