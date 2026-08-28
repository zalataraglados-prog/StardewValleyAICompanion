using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FarmComputerMainlineTests
{
    private const string OptionId = "farming.read_farm_computer_report";

    [Fact]
    public void FarmComputerClosesFiveGatesButRemainsPlayerCommandOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-280" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-280" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-280" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-280" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-280" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void OnlyExplicitConfirmedPlayerCommandCanPublishReportCandidate()
    {
        var snapshot = Snapshot();
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == OptionId);

        var explicitCommand = Assert.Single(evaluator.Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = OptionId,
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true
                }
            },
            includeExecutorCalibrationOptions: true).Options);

        Assert.Equal("authorized", explicitCommand.ExecutionAuthorization);
        var candidate = Assert.Single(explicitCommand.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, p => p.Name == "farm_computer_total_crops" && p.Value == "12");
        Assert.Contains(candidate.Parameters, p => p.Name == "farm_computer_report_sha256" && p.Value == "report-sha");
    }

    [Fact]
    public void CompilerRebindsExactNativeReportAndRejectsStaleTarget()
    {
        var snapshot = Snapshot();
        var action = Action(snapshot, new[]
        {
            P("target_tile_x", "20"), P("target_tile_y", "20"),
            P("stand_tile_x", "999"), P("stand_tile_y", "999"),
            P("farm_computer_total_crops", "999"),
            P("farm_computer_report_sha256", "forged"),
            P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_x" && p.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_y" && p.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "farm_computer_total_crops" && p.Value == "12");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "farm_computer_report_sha256" && p.Value == "report-sha");
        Assert.Equal("read_farm_computer_report", Assert.Single(item.NormalizedCommand.Steps).StepType);

        var stale = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);
        Assert.Contains("farm_computer_projection_drifted", stale.BlockingReasons);
        Assert.Empty(stale.NormalizedCommand.Steps);
    }

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "farm-computer.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.farm-computer",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.main",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "read.farm-computer",
                OptionId = OptionId,
                Rationale = "explicit player request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"), tile_x = Field(18), tile_y = Field(19),
                safe_item_context = Field(new
                {
                    current_tool_index = 2, active_object_selected = true,
                    safe_slot_available = true, safe_slot_index = 5, safe_slot_kind = "empty",
                    policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) },
            current_location = new
            {
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 20, tile_y = 20, item_id = "239", qualified_item_id = "(BC)239",
                        parent_sheet_index = 239, big_craftable = true, name = "Farm Computer",
                        type = "StardewValley.Object", object_type = "Crafting",
                        farm_computer_report = new
                        {
                            status = "ready", root_location_id = "Farm", root_location_display_name = "Farm",
                            includes_hay = true, pieces_of_hay = 120, hay_capacity = 240,
                            total_crops = 12, crops_ready_for_harvest = 3, unwatered_crops = 4,
                            greenhouse_crops_ready_for_harvest = 2, includes_greenhouse_line = true,
                            total_open_hoe_dirt = 5, total_forage_items = 6, includes_forage_line = true,
                            machines_ready_for_harvest = 7, farm_cave_needs_harvesting = false,
                            includes_farm_cave_line = true, report_text = "Farm analysis^...",
                            report_sha256 = "report-sha", expected_delay_milliseconds = 500,
                            expected_shake_timer_immediately_after_action = 500,
                            expected_player_freeze_milliseconds = 500,
                            expected_native_location_action_return = true,
                            target_runtime_type = "StardewValley.Object",
                            canonical_item_id = "239", canonical_qualified_item_id = "(BC)239",
                            stand_tiles = new[]
                            {
                                new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = false, available = true }
                            },
                            has_available_adjacent_stand = true,
                            interaction_kind = "location_object", expected_action_type = "FarmComputer",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)239->CheckForActionOnFarmComputer->delay_500ms->ShowFarmComputerReport->Game1.multipleDialogues"
                        }
                    }
                })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-29T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static object Field(object value) => new
    {
        value, status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test", read_at_tick = 1, confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
