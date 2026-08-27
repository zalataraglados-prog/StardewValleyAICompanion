using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DwarfKingStatueMainlineTests
{
    [Fact]
    public void DwarfKingStatueChoiceClosesFiveEvidenceGatesAndIsTrainingEligible()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("mining.choose_dwarf_statue_power");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-269" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-269" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-269" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-269" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-269" }, declaration.OutputEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
    }

    [Fact]
    public void ExactTwoDailyOffersBecomeDistinctModelChoiceCandidatesAndOneNativeStep()
    {
        var snapshot = Snapshot("ready", offeredPowerIds: new[] { 1, 4 });
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "mining.choose_dwarf_statue_power" },
            includeExecutorCalibrationOptions: true);
        var candidates = availability.Options.Single().EventCandidates;

        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate => Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons)));
        Assert.Equal(new[] { "1", "4" }, candidates
            .Select(candidate => candidate.Parameters.Single(parameter => parameter.Name == "dwarf_statue_power_id").Value)
            .ToArray());

        var selected = candidates[1];
        var ranked = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = selected.CandidateId,
                OptionId = "mining.choose_dwarf_statue_power",
                Kind = selected.Kind,
                Available = selected.Available,
                LocationId = selected.LocationId,
                TileX = selected.TileX,
                TileY = selected.TileY,
                ExpectedEffect = selected.ExpectedEffect,
                EstimatedTicks = selected.EstimatedTicks,
                Parameters = selected.Parameters
            }
        };
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("choose_dwarf_statue_power", planStep.Kind);
        Assert.Contains(planStep.Parameters, parameter => parameter.Name == "dwarf_statue_power_id" && parameter.Value == "4");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("choose_dwarf_statue_power", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "dwarf_statue_power_id" && parameter.Value == "4");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "dwarf_statue_menu_index" && parameter.Value == "1");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "19");
    }

    [Theory]
    [InlineData(null, "dwarf_statue_power_id_0_4_required_from_small_model")]
    [InlineData(3, "dwarf_statue_power_id_not_in_exact_daily_offers")]
    public void MissingOrUnofferedModelChoiceFailsClosed(int? powerId, string expectedReason)
    {
        var snapshot = Snapshot("ready", offeredPowerIds: new[] { 1, 4 });
        var parameters = powerId.HasValue
            ? new[] { new SmallModelActionParameter { Name = "dwarf_statue_power_id", Value = powerId.Value.ToString() } }
            : Array.Empty<SmallModelActionParameter>();
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "dwarf-statue.invalid",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "choose.dwarf.power",
                    OptionId = "mining.choose_dwarf_statue_power",
                    Rationale = "select daily mining power",
                    Parameters = parameters
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons, reason => reason == expectedReason);
    }

    [Fact]
    public void ExistingDailyBuffIsExcludedUpstreamAndRejectedByCompiler()
    {
        var snapshot = Snapshot("already_chosen_today", offeredPowerIds: new[] { 1, 4 });
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "mining.choose_dwarf_statue_power" },
            includeExecutorCalibrationOptions: true);

        Assert.All(availability.Options.Single().EventCandidates, candidate =>
        {
            Assert.False(candidate.Available);
            Assert.Contains(candidate.BlockReasons, reason => reason.StartsWith("dwarf_king_statue_not_ready", StringComparison.Ordinal));
        });
    }

    private static SnapshotEnvelope Snapshot(string status, int[] offeredPowerIds)
    {
        var offers = offeredPowerIds.Select((id, index) => new
        {
            menu_index = index,
            power_id = id,
            buff_id = "dwarfStatue_" + id,
            display_text = "Power " + id,
            effect = new { kind = "effect_" + id, exact_effect = "exact_" + id, source = "decompile" }
        }).ToArray();
        var projection = new
        {
            status,
            location_id = "Farm",
            mining_mastery_value = 1,
            mining_mastery_unlocked = true,
            days_played = 42,
            offered_power_ids = offeredPowerIds,
            offered_power_ids_csv = string.Join(",", offeredPowerIds),
            offers,
            has_active_dwarf_statue_buff = status == "already_chosen_today",
            statues = new[]
            {
                new
                {
                    tile_x = 20,
                    tile_y = 20,
                    qualified_item_id = "(BC)StatueOfTheDwarfKing",
                    target_runtime_type = "StardewValley.Object",
                    stand_tiles = new[]
                    {
                        new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, available = true },
                        new { tile_x = 19, tile_y = 20, on_map = true, collision_blocked = false, available = true }
                    }
                }
            },
            qualified_item_id = "(BC)StatueOfTheDwarfKing",
            expected_menu_type = "ChooseFromIconsMenu",
            native_contract = "Object.checkForAction_StatueOfTheDwarfKing->ChooseFromIconsMenu(dwarfStatue)->receiveLeftClick_exact_offered_icon->Farmer.applyBuff(dwarfStatue_N)"
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"),
                tile_x = Field(18),
                tile_y = Field(19)
            },
            menus = new
            {
                active_menu = Field(new { is_open = false, type = "none" })
            },
            current_location = new
            {
                dwarf_king_statue_power = Field(projection)
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-27T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
