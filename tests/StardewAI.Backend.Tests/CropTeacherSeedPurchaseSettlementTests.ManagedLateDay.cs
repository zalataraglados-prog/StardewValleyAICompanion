using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedLateDayRoutesThenSavesThroughTheSameOwnerWithoutInventingCropProgress()
    {
        // Declared CLR/HTTP and named save fixture only, not vanilla growth/native-game evidence.
        using var f = new OvernightFixture(noCrops: true, prepareSource: source => SetManagedLateDayClock(source, 1900));
        await using var owner = await f.Open();
        var initialSource = owner.CurrentSnapshotPath;
        var interrupted = f.Watering.Basis.Step with
        {
            CommitmentId = Id, TargetLocationId = "InterruptedCropTarget", HomeLocationId = "NotTheReadableHome",
            SeedQualifiedItemId = "(O)745", ShopId = "SeedShop", MaxUnitPrice = 80, RemainingQuantity = 2,
            SeedReservationId = "interrupted-seed-reservation"
        };
        Assert.NotNull(interrupted.ActionBatch);
        var route = await SelectManagedLateDay(f.Shared, owner, interrupted);
        AssertLateDayOverride(route, CropTeacherRunStageKinds.RouteHome, interrupted.TotalDay);
        Assert.True(route.Operation.IsSupportingRoute);
        Assert.Same(route.Operation, owner.Pending);
        Assert.Equal(initialSource, route.SourcePath);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        var routeQueue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.Shared.QueuePath), JsonOptions)!;
        Assert.Equal("executor.traverse_connector", Assert.Single(routeQueue.Items).OptionId);
        Assert.Contains(routeQueue.Items[0].NormalizedCommand.Parameters,
            row => row.Name == "cross_location_move_target" && row.Value == "FarmHouse");
        Assert.False(File.Exists(f.Watering.QueuePath));
        var routeCalls = 0;
        await owner.ExecuteOriginalRouteAsync(route.Operation, f.Shared.Runtime.Client, () =>
        {
            routeCalls++;
            return f.Shared.EmitNative(sourcePath: initialSource, change: snapshot => ChangeState(snapshot, "current_location",
                node => node["home_context"]!["value"]!["current_location_is_home"] = true));
        });
        Assert.Equal(initialSource, owner.CurrentSnapshotPath);
        await f.Shared.Rebind(owner, route.Operation);
        var homeSource = owner.CurrentSnapshotPath;
        Assert.Equal(f.Shared.AfterPath, homeSource);
        Assert.Null(owner.Pending);
        var night = await SelectManagedLateDay(f.Shared, owner, interrupted, iteration: 3);
        AssertLateDayOverride(night, CropTeacherRunStageKinds.NativeSleep, interrupted.TotalDay);
        Assert.True(night.Operation.IsOvernight);
        Assert.Equal(homeSource, night.SourcePath);
        Assert.Same(night.Operation, owner.Pending);
        Assert.Equal("declared-before-native-night", File.ReadAllText(f.NativeSaveFile));
        var sleepQueue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.NightQueue), JsonOptions)!;
        Assert.Equal("executor.sleep", Assert.Single(sleepQueue.Items).OptionId);
        var sleepCandidate = sleepQueue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "precondition" &&
            row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
        Assert.NotEqual("recovery:native_save_boundary", sleepCandidate);
        await owner.ExecuteOriginalOvernightAsync(night.Operation, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        Assert.Equal(homeSource, owner.CurrentSnapshotPath);
        await f.RebindNight(owner, night.Operation);
        Assert.Equal(1, routeCalls); Assert.Equal(1, f.NativeNightCalls); Assert.Equal(0, f.NativeHarvestCalls);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits); Assert.Equal(0, f.Shared.Runtime.Handler.Settlements);
        Assert.Null(owner.Pending); Assert.Equal(f.NightAfter, owner.CurrentSnapshotPath);
        var accepted = ReadManagedLateDayAccepted(f.Shared, night.Operation);
        Assert.True(accepted["native_overnight_control_verified"]!.GetValue<bool>());
        Assert.True(accepted["native_save_boundary_verified"]!.GetValue<bool>());
        Assert.Equal(0, accepted["verified_crop_tile_count"]!.GetValue<int>());
        Assert.Equal(2, accepted["unverified_layout_tile_count"]!.GetValue<int>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        Assert.True(ReadManagedLateDayAccepted(f.Shared, route.Operation)["route_target_reached"]!.GetValue<bool>());
        Assert.True(File.Exists(Path.Combine(route.Operation.RuntimeDirectory, "route-native-claim.json")));
        Assert.True(File.Exists(Path.Combine(night.Operation.RuntimeDirectory, "overnight-native-claim.json")));
        Assert.True(File.Exists(Path.Combine(night.Operation.RuntimeDirectory, "overnight-save-boundary.json")));
        Assert.Equal("declared-completed-native-night", File.ReadAllText(f.NativeSaveFile));
        var morning = JsonNode.Parse(File.ReadAllText(owner.CurrentSnapshotPath))!;
        Assert.Equal(interrupted.TotalDay + 1, morning["state"]!["time"]!["total_days"]!["value"]!.GetValue<int>());
        Assert.Equal(600, morning["state"]!["time"]!["time"]!["value"]!.GetValue<int>());
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.False(reopened.HeadRecoveryRequired); Assert.Equal(f.NightAfter, reopened.CurrentSnapshotPath);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits); Assert.Equal(1, f.NativeNightCalls);
    }

    [Fact]
    public async Task ManagedLateDayCannotBypassPendingMarkerRecoveryOrAcceptedHeadReadback()
    {
        using var f = new OwnedRouteFixture(prepareSource: source => SetManagedLateDayClock(source, 1900));
        var loseReply = true;
        await using var owner = await f.Files.Open(writer: async (path, json) =>
        {
            if (loseReply && Path.GetFileName(path) == "marker-reply.json")
            { loseReply = false; throw new IOException("Late-day marker committed before local acknowledgement."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var selected = await SelectManagedLateDay(f, owner, f.Step);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SelectManagedLateDay(f, owner, f.Step, 2));
        Assert.False(File.Exists(Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "crop-teacher-route-0002-compiled-queue.json")));
        var calls = 0;
        await owner.ExecuteOriginalRouteAsync(selected.Operation, f.Runtime.Client, () => { calls++; return f.EmitNative(); });
        await Assert.ThrowsAsync<IOException>(() => f.Rebind(owner, selected.Operation));
        await owner.DisposeAsync();
        await using var recovering = await f.Files.Open();
        Assert.NotNull(recovering.Pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SelectManagedLateDay(f, recovering, f.Step, 2));
        await recovering.RecoverPendingAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        Assert.Null(recovering.Pending);
        await recovering.DisposeAsync();
        await using var readback = await f.Files.Open();
        Assert.True(readback.HeadRecoveryRequired);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SelectManagedLateDay(f, readback, f.Step, 2));
        await readback.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery + "-head", f.Files.Post);
        Assert.False(readback.HeadRecoveryRequired); Assert.Equal(f.AfterPath, readback.CurrentSnapshotPath);
        Assert.Equal(1, calls); Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Lookups);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(1850)]
    public async Task ManagedLateDayEarlyClockRejectsDirectRecoveryButPreservesNormalSelector(int time)
    {
        using var f = new OwnedRouteFixture(prepareSource: source => SetManagedLateDayClock(source, time));
        await using var owner = await f.Files.Open();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => SelectManagedLateDay(f, owner, f.Step, direct: true));
        Assert.Contains("clock", error.Message);
        Assert.Null(owner.Pending); Assert.False(File.Exists(f.QueuePath));
        var normal = await SelectManagedLateDay(f, owner, f.Step);
        Assert.Equal(string.Empty, normal.Interrupt); Assert.Null(normal.Override);
        Assert.True(normal.Operation.IsSupportingRoute); Assert.Same(normal.Operation, owner.Pending);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedLateDayMissingReadableHomeRejectsBeforeCompilationOrAdmission(bool unavailable)
    {
        using var f = new OwnedRouteFixture(prepareSource: source =>
        {
            SetManagedLateDayClock(source, 1900);
            ChangeState(source.Before, "current_location", node =>
            {
                if (unavailable) node["home_context"]!["status"] = "unavailable";
                else node["home_context"]!["value"]!["home_location_id"] = "";
            });
        });
        await using var owner = await f.Files.Open();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => SelectManagedLateDay(f, owner, f.Step));
        Assert.Contains("readable original home", error.Message);
        Assert.Null(owner.Pending); Assert.False(File.Exists(f.QueuePath));
        Assert.Empty(Directory.GetFiles(f.Files.OwnerRoot, "operation-*-intent.json"));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedLateDayStoryAndSafeDialogueRemainHigherPriorityThanRecovery(bool dialogue)
    {
        using var f = new OwnedRouteFixture(prepareSource: source =>
        {
            SetManagedLateDayClock(source, 1900);
            if (dialogue)
                source.Before.State["menus"] = JsonSerializer.SerializeToElement(new
                {
                    active_menu = new { status = "available", value = new
                    {
                        is_open = true, type = "DialogueBox", is_sleep_prompt = false, event_up = false,
                        dialogue_is_question = false, dialogue_response_count = 0, dialogue_transitioning = false,
                        dialogue_character_present = false, dialogue_speaker_name = "", last_question_key = ""
                    } },
                    sleep_prompt_context = new { status = "available", value = new { prompt_open = false } }
                });
            else ChangeState(source.Before, "player", node => node["story_event"] = JsonSerializer.SerializeToNode(new
            {
                status = "available", value = new { active = true, event_up = true, is_festival = false,
                    skipped = false, player_control_sequence = false, active_minigame_type = "", event_id = "declared-late-day-story" }
            }));
        });
        await using var owner = await f.Files.Open();
        var source = JsonNode.Parse(File.ReadAllText(owner.CurrentSnapshotPath))!.AsObject();
        Assert.True(dialogue ? CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(source)
            : InvokeLive<bool>("HasPriorityStoryEventInterrupt", source));
        if (dialogue)
        {
            var selected = await SelectManagedLateDay(f, owner, f.Step);
            Assert.True(selected.Operation.IsControlRecovery);
            Assert.Equal("safe_dialogue_recovery", selected.Interrupt);
            Assert.Null(selected.Override); Assert.Same(selected.Operation, owner.Pending);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => SelectManagedLateDay(f, owner, f.Step));
            Assert.Contains("control interrupt", error.Message);
            Assert.Null(owner.Pending);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => SelectManagedLateDay(f, owner, f.Step, direct: true));
        Assert.False(File.Exists(f.QueuePath)); Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    private static void SetManagedLateDayClock(Fixture source, int time)
    {
        source.Before.InGameTime.Value = time;
        ChangeState(source.Before, "time",
            node => node["time"] = JsonSerializer.SerializeToNode(new { status = "available", value = time }));
    }

    private static JsonNode ReadManagedLateDayAccepted(OwnedRouteFixture f, CropTeacherManagedSeedOperation operation) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot, $"operation-{operation.Sequence:D6}-accepted.json")))!;

    private static void AssertLateDayOverride((CropTeacherManagedSeedOperation Operation, CropTeacherRunStep? Override,
        string Interrupt, string SourcePath) selected, string stage, int day)
    {
        Assert.Equal("crop_teacher_late_day_recovery", selected.Interrupt);
        var step = Assert.IsType<CropTeacherRunStep>(selected.Override);
        Assert.Equal(stage, step.Stage); Assert.Equal(day, step.TotalDay);
        Assert.Equal("FarmHouse", step.TargetLocationId); Assert.Equal("FarmHouse", step.HomeLocationId);
        Assert.Equal(string.Empty, step.CommitmentId); Assert.Equal(string.Empty, step.SeedQualifiedItemId);
        Assert.Equal(string.Empty, step.ShopId); Assert.Equal(0, step.MaxUnitPrice); Assert.Equal(0, step.RemainingQuantity);
        Assert.Null(step.ActionBatch); Assert.Null(step.SeedTransferIntent); Assert.Null(step.SeedReservationId);
        Assert.Equal("crop_teacher_late_day_recovery", step.Reason);
    }

    private static async Task<(CropTeacherManagedSeedOperation Operation, CropTeacherRunStep? Override, string Interrupt, string SourcePath)>
        SelectManagedLateDay(OwnedRouteFixture f, CropTeacherManagedSeedOwner owner, CropTeacherRunStep step,
            int iteration = 1, bool direct = false)
    {
        var task = InvokeLive<Task>(direct ? "BuildCropTeacherOwnedLateDayQueueAsync" : "BuildCropTeacherOwnedControlQueueAsync",
            f.Runtime.Client, f.Runtime.LiveOptions, iteration, step, f.Inputs.Plan, f.Inputs.Request, owner);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var operation = (CropTeacherManagedSeedOperation)result.GetType().GetProperty("Operation")!.GetValue(result)!;
        var build = result.GetType().GetProperty("Build")!.GetValue(result)!;
        return (operation, (CropTeacherRunStep?)build.GetType().GetProperty("ExecutionStepOverride")!.GetValue(build),
            (string)build.GetType().GetProperty("InterruptKind")!.GetValue(build)!,
            (string)build.GetType().GetProperty("BeforeSnapshotPath")!.GetValue(build)!);
    }
}
