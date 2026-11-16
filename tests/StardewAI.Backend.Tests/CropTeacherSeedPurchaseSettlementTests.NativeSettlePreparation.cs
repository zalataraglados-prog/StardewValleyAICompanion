using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task NativeSettlePreparationObservesFarmWithoutClaimingHomeReached()
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var planPath = await BuildNativeSettlePreparation(f);
        var preparation = OpenNativeSettlePreparation(f, planPath);
        Assert.Equal("Farm", preparation.ExpectedHopTargetLocation);
        Assert.Equal("FarmHouse", preparation.FinalTargetLocation);
        Assert.Equal(NativeControlSettleRecovery.CandidateId, preparation.CandidateId);
        Assert.Equal(f.Step.TotalDay, preparation.TotalDay);
        Assert.Equal(5, preparation.InputHashes.Count);
        Assert.Contains(Path.GetFullPath(planPath), preparation.InputHashes.Keys);
        var route = preparation.VerifyTransition(CreateNativeSettleTransition(f));
        Assert.False(route.RouteTargetReached);
        Assert.False(route.NativeCropLifecycleVerified);
        Assert.False(route.FormalProductTrainingAuthorized);
        // Both plan and queue remain pinned across native verification.
        File.AppendAllText(planPath, " ");
        Assert.Throws<InvalidDataException>(preparation.ValidateInputsUnchanged);
    }

    [Fact]
    public async Task NativeSettlePreparationDefaultStillRejectsWait()
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var planPath = await BuildNativeSettlePreparation(f);
        Assert.Throws<InvalidDataException>(() => CropTeacherSupportingRoute.Preparation.Open(
            f.Source.RequestPath, f.Source.SourcePath, f.Source.LedgerPath, new[] { Id }, "native.settle.step",
            "supporting_route_to_sleep", planPath, f.QueuePath));
    }

    [Theory]
    [InlineData("generic-wait")]
    [InlineData("wrong-candidate")]
    [InlineData("wrong-ticks")]
    [InlineData("no-replan")]
    [InlineData("wrong-state")]
    [InlineData("wrong-execution")]
    [InlineData("replay-tamper")]
    public async Task NativeSettlePreparationRejectsChangedOriginalScope(string fault)
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var planPath = await BuildNativeSettlePreparation(f);
        var plan = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(File.ReadAllText(planPath), JsonOptions)!;
        switch (fault)
        {
            case "generic-wait": plan.SourceModel = "generic_wait"; break;
            case "wrong-candidate":
                plan.Steps[0].Preconditions = new[] { "candidate_id:other-wait" };
                plan.CandidateAudit[0].CandidateId = "other-wait";
                break;
            case "wrong-ticks": plan.Steps[0].WaitTicks = 31; break;
            case "no-replan": plan.Steps[0].FailurePolicy = Array.Empty<string>(); break;
            case "wrong-state": plan.StateHash = "another-state"; break;
            case "wrong-execution": plan.ExecutionMode = "not-native-singleplayer"; break;
        }
        if (fault == "replay-tamper")
        {
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.QueuePath), JsonOptions)!;
            queue.Items[0].NormalizedCommand.Steps[0].Target = "31";
            Fixture.Save(f.QueuePath, queue);
        }
        else
        {
            Fixture.Save(planPath, plan);
            // A self-consistent recompilation still cannot admit an altered plan scope.
            if (fault != "wrong-execution")
                Fixture.Save(f.QueuePath, new ActionQueueCompiler().Compile(plan, f.Source.Before));
        }
        Assert.Throws<InvalidDataException>(() => OpenNativeSettlePreparation(f, planPath));
    }

    [Fact]
    public async Task NativeSettlePreparationRejectsNonSleepKindAndUnavailableNativeGap()
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var planPath = await BuildNativeSettlePreparation(f);
        Assert.Throws<InvalidDataException>(() => OpenNativeSettlePreparation(f, planPath, "supporting_route_to_crop"));
        ChangeState(f.Source.Before, "player", node => node["movement_timing_context"]!["value"]!["route_timing_ready_now"] = true);
        Fixture.Save(f.Source.SourcePath, f.Source.Before);
        Assert.Null(NativeControlSettleRecovery.TryCreate(f.Source.Before));
        Assert.Throws<InvalidDataException>(() => OpenNativeSettlePreparation(f, planPath));
    }

    [Theory]
    [InlineData("resources")]
    [InlineData("position")]
    [InlineData("short-ticks")]
    [InlineData("day")]
    public async Task NativeSettlePreparationRetainsOriginalNativeProof(string fault)
    {
        using var f = new OwnedRouteFixture(prepareSource: PrepareNativeSettleSource);
        var planPath = await BuildNativeSettlePreparation(f);
        var preparation = OpenNativeSettlePreparation(f, planPath);
        var transition = CreateNativeSettleTransition(f, fault);
        Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(transition));
    }

    private static CropTeacherSupportingRoute.Preparation OpenNativeSettlePreparation(
        OwnedRouteFixture f, string planPath, string kind = "supporting_route_to_sleep") =>
        CropTeacherSupportingRoute.Preparation.Open(f.Source.RequestPath, f.Source.SourcePath, f.Source.LedgerPath,
            new[] { Id }, "native.settle.step", kind, planPath, f.QueuePath, nativeControlObservation: true);

    private static async Task<string> BuildNativeSettlePreparation(OwnedRouteFixture f)
    {
        var planPath = RoutePath(f.Source, "native-settle-plan.json");
        await InvokeLive<Task>("TryBuildNativeControlSettleQueueAsync", f.Runtime.LiveOptions,
            File.ReadAllText(f.Source.SourcePath), f.Source.Before.StateHash, f.Source.SourcePath,
            RoutePath(f.Source, "native-settle-preference.json"), planPath,
            RoutePath(f.Source, "native-settle-ranking.json"), f.QueuePath);
        Assert.True(File.Exists(planPath), "Original native-control settle builder did not produce its plan.");
        Assert.True(File.Exists(f.QueuePath));
        return planPath;
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest CreateNativeSettleTransition(OwnedRouteFixture f, string fault = "")
    {
        var before = f.Source.Before;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        after.GameTick += fault == "short-ticks" ? 1 : NativeControlSettleRecovery.WaitTicks;
        ChangeState(after, "player", node =>
        {
            node["movement_timing_context"]!["value"]!["player_can_move_now"] = true;
            node["movement_timing_context"]!["value"]!["route_timing_ready_now"] = true;
            if (fault == "resources") node["money"]!["value"] = 1;
            if (fault == "position") node["tile_x"]!["value"] = 4;
        });
        if (fault == "day") ChangeState(after, "time", node =>
            node["total_days"]!["value"] = node["total_days"]!["value"]!.GetValue<int>() + 1);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.QueuePath), JsonOptions)!;
        var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
            f.Files.Options.RunId, NativeControlSettleRecovery.CandidateId, "wait_ticks");
        receipt.StepResults[0].AfterGameTick = after.GameTick;
        Fixture.Save(f.AfterPath, after);
        Fixture.Save(f.ReceiptPath, receipt);
        return new("supporting_route_to_sleep", f.Step.TotalDay, f.QueuePath, f.Source.SourcePath,
            f.ReceiptPath, f.AfterPath, receipt.RunId, f.Files.Options.ExecutorVersion, NativeControlSettleRecovery.CandidateId);
    }

    private static void PrepareNativeSettleSource(Fixture f)
    {
        ChangeState(f.Before, "player", node => node["movement_timing_context"] = JsonSerializer.SerializeToNode(new
        {
            status = "available",
            value = new
            {
                projection_status = "exact_current_player_native_cardinal_movement_context",
                scope = "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                runtime_calibration_compatible = true, route_timing_ready_now = false,
                player_using_tool_now = false, immobilizing_buff_19_active = false,
                current_event_active = false, event_up = false, game_paused = false,
                freeze_controls = false, is_warping = false, dialogue_up = false,
                player_freeze_pause = 0, native_control_gate_open = true,
                player_can_move_now = false, global_fade = false
            }
        }));
        f.Before.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new { is_open = false } }
        });
    }
}
