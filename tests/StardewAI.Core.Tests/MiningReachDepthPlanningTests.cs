using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class MiningReachDepthPlanningTests
{
    [Fact]
    public void OptionRegistryRegistersMiningReachDepthAsParameterizedMechanical()
    {
        var option = new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired("mining.reach_depth");

        Assert.Equal("mining", option.Domain);
        Assert.Equal(OptionBehaviorCategories.ParameterizedMechanical, option.BehaviorCategory);
        Assert.Equal(CompilerResponsibilities.ParameterExpansion, option.CompilerResponsibility);
        Assert.Contains("mining.current_mine", option.RequiredStateFactors);
        Assert.Contains("mining.player_resources", option.RequiredStateFactors);
    }

    [Fact]
    public void AvailabilityBuildsReachDepthCandidateOnlyFromCompleteMiningState()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines");
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = "mining.reach_depth",
                Parameters = new[] { Parameter("target_depth", "45"), Parameter("target_location_family", "ordinary_mines") }
            }
        });

        var option = Assert.Single(availability.Options);
        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("mining_reach_depth_plan_envelope", candidate.Kind);
        Assert.Equal(-1, candidate.EstimatedTicks);
        Assert.Equal(-1, candidate.EnergyCost);
        Assert.Equal("available_rolling_horizon_floor_step", candidate.AvailabilityClass);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "elevator_start_depth" && parameter.Value == "45");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "estimate_status" && parameter.Value == "rolling_horizon_current_floor_step");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "runtime_boundary" && parameter.Value == "current_floor_step_executable");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.mine_stone");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "minimum_reserve_health" && parameter.Value == string.Empty);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "minimum_reserve_energy" && parameter.Value == string.Empty);
    }

    [Fact]
    public void AvailabilityFailsClosedWhenMiningGroupsAreUnavailable()
    {
        var snapshot = Snapshot("""
        {
          "mining": {
            "current_mine": {"value":null,"status":"unavailable","source":{"kind":"unavailable","path":"test"},"adapter":"test","read_at_tick":1,"confidence":0,"reason":"not_loaded_mineshaft"}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "mining.reach_depth" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Contains("missing_required_state", option.BlockingReasons);
        Assert.Empty(option.EventCandidates);
    }

    [Fact]
    public void CompilerPreservesReachDepthGoalAndExpandsCurrentFloorPrimitive()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines");
        var request = Request(snapshot.StateHash);
        request.Actions[0].Parameters = new[]
        {
            Parameter("target_depth", "45"),
            Parameter("target_location_family", "ordinary_mines"),
            Parameter("latest_exit_time", "2400"),
            Parameter("minimum_reserve_health", "25"),
            Parameter("minimum_reserve_energy", "20"),
            Parameter("resource_preservation_policy", "preserve_staircases")
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("mining.reach_depth", item.OptionId);
        Assert.Equal("option_request", item.NormalizedCommand.CommandType);
        Assert.Empty(item.NormalizedCommand.Steps);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_depth" && parameter.Value == "45");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "current_depth" && parameter.Value == "40");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "runtime_boundary" && parameter.Value == "current_floor_step_executable");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.mine_stone");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "3");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_y" && parameter.Value == "2");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "resource_preservation_policy" && parameter.Value == "preserve_staircases");
    }

    [Fact]
    public void CompilerBlocksKnownImpossibleTargetBeforeRuntime()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines");
        var request = Request(snapshot.StateHash);
        request.Actions[0].Parameters = new[] { Parameter("target_depth", "130"), Parameter("target_location_family", "ordinary_mines") };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("ordinary_mine_target_depth_out_of_range", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedForNestedUnavailableMiningFacts()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines", collisionStatus: "unavailable");

        var option = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = "mining.reach_depth",
                Parameters = new[] { Parameter("target_depth", "45"), Parameter("target_location_family", "ordinary_mines") }
            }
        }).Options[0];

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("mining.tiles.collision_context", option.BlockingReasons);
        Assert.Contains("no_mining_reach_depth_candidates", option.BlockingReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedForUnavailableObjectGroup()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines", objectsStatus: "unavailable");

        var option = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = "mining.reach_depth",
                Parameters = new[] { Parameter("target_depth", "45"), Parameter("target_location_family", "ordinary_mines") }
            }
        }).Options[0];

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("mining.objects", option.BlockingReasons);
        Assert.Contains("no_mining_reach_depth_candidates", option.BlockingReasons);
    }

    [Theory]
    [InlineData("Mine", "Mine", true)]
    [InlineData("Mine 40", "Mine", true)]
    [InlineData("MineElevator", "Mine", false)]
    [InlineData("MineElevator", "MineElevator", true)]
    public void MiningActionParsingUsesExactFirstToken(string action, string token, bool expected)
    {
        Assert.Equal(expected, MiningReadAdapter.ActionTokenEquals(action, token));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(4, 0, 5)]
    [InlineData(4, 2, 7)]
    public void MiningPickaxeDamageMatchesDecompiledDoFunction(int upgradeLevel, int additionalPower, int expected)
    {
        Assert.Equal(expected, MiningReadAdapter.PickaxeDamagePerHit(upgradeLevel, additionalPower));
    }

    [Theory]
    [InlineData(16, 1, 16)]
    [InlineData(16, 2, 8)]
    [InlineData(16, 5, 4)]
    [InlineData(0, 5, 0)]
    public void MiningRemainingHitsUsesLiveStoneMinutesUntilReady(int health, int damage, int expected)
    {
        Assert.Equal(expected, MiningReadAdapter.RemainingHits(health, damage));
    }

    [Fact]
    public void MiningLadderChanceMatchesDecompiledFormula()
    {
        var chance = MiningReadAdapter.LadderChanceAfterBreak(
            stonesBeforeBreak: 11,
            luckLevel: 3,
            dailyLuck: 0.1,
            enemyCount: 0,
            dwarfStatueBuff: false);

        Assert.Equal(0.21, chance, precision: 10);
        Assert.Equal(1.3875, MiningReadAdapter.LadderChanceAfterBreak(1, 3, 0.1, 0, true), precision: 10);
    }

    [Fact]
    public void MiningAdapterNoLongerPublishesKnownRequiredFactsAsUnavailable()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MiningReadAdapter.cs"));

        Assert.DoesNotContain("map_collision_passability_unavailable", source, StringComparison.Ordinal);
        Assert.DoesNotContain("object_classification_incomplete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("floor_constraints_incomplete", source, StringComparison.Ordinal);
        Assert.Contains("Object.IsBreakableStone/MinutesUntilReady", source, StringComparison.Ordinal);
        Assert.Contains("MineShaft.checkStoneForItems exact seed", source, StringComparison.Ordinal);
        Assert.Contains("resilience = monster.resilience.Value", source, StringComparison.Ordinal);
        Assert.Contains("runtime_identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster)", source, StringComparison.Ordinal);
        Assert.Contains("miss_chance = monster.missChance.Value", source, StringComparison.Ordinal);
        Assert.Contains("is_invincible = monster.isInvincible()", source, StringComparison.Ordinal);
        Assert.Contains("weapon_slots = player.Items.Select", source, StringComparison.Ordinal);
        Assert.Contains("min_damage = weapon.minDamage.Value", source, StringComparison.Ordinal);
        Assert.Contains("critical_chance = weapon.critChance.Value", source, StringComparison.Ordinal);
        Assert.Contains("attack_multiplier = player.buffs.AttackMultiplier", source, StringComparison.Ordinal);
        Assert.Contains("attack = player.Attack", source, StringComparison.Ordinal);
        Assert.Contains("luck_level = player.LuckLevel", source, StringComparison.Ordinal);
        Assert.Contains("professions = player.professions.OrderBy", source, StringComparison.Ordinal);
        Assert.Contains("player_enchantments = player.enchantments.Select", source, StringComparison.Ordinal);
        Assert.Contains("equipped_trinkets = player.trinketItems.Select", source, StringComparison.Ordinal);
        Assert.Contains("melee_damage_semantics = ReadMeleeDamageSemantics", source, StringComparison.Ordinal);
        Assert.Contains("case Spiker:", source, StringComparison.Ordinal);
        Assert.Contains("requires_bug_killer_enchantment", source, StringComparison.Ordinal);
        Assert.Contains("mummy_revives_without_crusader_or_bomb", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentBridgeHasPurposeLimitedMiningSnapshotProfile()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));

        Assert.Contains("or \"mining\" or \"full\"", source, StringComparison.Ordinal);
        Assert.Contains("if (profile is \"mining\")", source, StringComparison.Ordinal);
        Assert.Contains("domains.Add(\"mining\")", source, StringComparison.Ordinal);

        var miningBlockStart = source.IndexOf("if (profile is \"mining\")", StringComparison.Ordinal);
        var miningBlockEnd = source.IndexOf("return domains;", miningBlockStart, StringComparison.Ordinal);
        var miningBlock = source[miningBlockStart..miningBlockEnd];
        Assert.DoesNotContain("domains.Add(\"current_location\")", miningBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("domains.Add(\"locations\")", miningBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void MiningRuntimeSmokeUsesIsolatedSilentPurposeLimitedSnapshot()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "Invoke-RuntimeMiningSnapshotSmoke.ps1"));

        Assert.Contains("E:\\StardewValleyAICompanion-runtime", source, StringComparison.Ordinal);
        Assert.Contains("profile=mining", source, StringComparison.Ordinal);
        Assert.Contains("debug.setup_mining_floor", source, StringComparison.Ordinal);
        Assert.Contains("$gameWindowStyle = if ($VisibleGame) { \"Normal\" } else { \"Hidden\" }", source, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("Assert-MiningSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("maximum_snapshot_latency_ms", source, StringComparison.Ordinal);

        var harnessSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        Assert.Contains("StartSetupMiningFloor", harnessSource, StringComparison.Ordinal);
        Assert.Contains("native_enter_mine_completed", harnessSource, StringComparison.Ordinal);
        Assert.Contains("loaded_mine_map_present", harnessSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveRuntimeUsesCompilerSelectedPrimitiveAndTypedCombatIdentity()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));

        Assert.Contains("ReadQueueParameterString(item, \"execution_option_id\")", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.TargetRuntimeIdentity = targetRuntimeIdentity", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.TargetRuntimeType = targetRuntimeType", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.TargetName = targetName", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.DebrisIndex = debrisIndex.Value", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.SlotIndex = slotIndex.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ElevatorStartUsesDeepestMineLevelInsteadOfTargetProgress()
    {
        Assert.Equal(5, MiningReachDepthCandidateBuilder.ElevatorStartFor(1, 40, "ordinary_mines", 5));
        Assert.Equal(45, MiningReachDepthCandidateBuilder.ElevatorStartFor(40, 45, "ordinary_mines", 120));
        Assert.Equal(40, MiningReachDepthCandidateBuilder.ElevatorStartFor(40, 45, "ordinary_mines", 40));
        Assert.Equal(52, MiningReachDepthCandidateBuilder.ElevatorStartFor(52, 60, "ordinary_mines", 52));
        Assert.Null(MiningReachDepthCandidateBuilder.ElevatorStartFor(40, 45, "ordinary_mines", null));
        Assert.Null(MiningReachDepthCandidateBuilder.ElevatorStartFor(130, 140, "skull_cavern", 120));
    }

    [Fact]
    public void OptionalReserveConstraintsAreAbsentUnlessModelProvidesThem()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines", health: 10, energy: 5);

        var candidate = Assert.Single(MiningReachDepthCandidateBuilder.Build(snapshot, new[]
        {
            Parameter("target_depth", "45"),
            Parameter("target_location_family", "ordinary_mines")
        }));

        Assert.DoesNotContain("minimum_reserve_health_not_met", candidate.BlockReasons);
        Assert.DoesNotContain("minimum_reserve_energy_not_met", candidate.BlockReasons);
        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ReachedReserveConstraintCompilesMandatoryRetreatInsteadOfStrandingActor()
    {
        var snapshot = MiningSnapshot(currentDepth: 40, targetFamily: "ordinary_mines", health: 10, energy: 5);

        var candidate = Assert.Single(MiningReachDepthCandidateBuilder.Build(snapshot, new[]
        {
            Parameter("target_depth", "45"),
            Parameter("target_location_family", "ordinary_mines"),
            Parameter("minimum_reserve_health", "20"),
            Parameter("minimum_reserve_energy", "10")
        }));

        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.exit_mine");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "mining_step_reason" && parameter.Value.Contains("minimum_reserve_energy_reached", StringComparison.Ordinal));
    }

    [Fact]
    public void ReachedOrOvershotTargetDepthCompilesMandatoryRetreat()
    {
        var snapshot = MiningSnapshot(currentDepth: 47, targetFamily: "ordinary_mines");

        var candidate = Assert.Single(MiningReachDepthCandidateBuilder.Build(snapshot, new[]
        {
            Parameter("target_depth", "45"),
            Parameter("target_location_family", "ordinary_mines")
        }));

        Assert.True(candidate.Available);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.exit_mine");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "mining_step_reason" && parameter.Value.Contains("target_depth_reached", StringComparison.Ordinal));
    }

    private static SnapshotEnvelope MiningSnapshot(int currentDepth, string targetFamily, string collisionStatus = "available", int health = 100, double energy = 220, string objectsStatus = "available", int deepestMineLevel = 120)
    {
        return Snapshot("""
        {
          "mining": {
            "current_mine": {"value":{"location_id":"UndergroundMine","mine_level":CURRENT_DEPTH,"mine_area":40,"mine_kind":"TARGET_FAMILY","is_loaded_current_location":true,"is_skull_cavern":false,"is_quarry_mine":false,"is_dangerous":false,"additional_difficulty":0},"status":"available","source":{"kind":"game_object","path":"MineShaft.mineLevel"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tiles": {"value":{"player_tile":{"tile_x":1,"tile_y":2},"collision_context":{"status":"COLLISION_STATUS","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100001","100001","111111"]},"exits":[{"tile_x":4,"tile_y":2,"tile_index":115,"expected_destination":{"location_id":"Mine","tile_x":23,"tile_y":8}}],"ladders":[],"shafts":[],"elevators":[]},"status":"available","source":{"kind":"game_object","path":"MineShaft.map"},"adapter":"test","read_at_tick":1,"confidence":1},
            "objects": {"value":[{"tile_x":3,"tile_y":2,"qualified_item_id":"(O)32","is_breakable_stone":true,"best_pickaxe_hits_remaining":2}],"status":"OBJECTS_STATUS","source":{"kind":"game_object","path":"MineShaft.objects"},"adapter":"test","read_at_tick":1,"confidence":1},
            "monsters": {"value":[],"status":"available","source":{"kind":"game_object","path":"MineShaft.characters"},"adapter":"test","read_at_tick":1,"confidence":1},
            "floor_objectives": {"value":{"must_kill_all_monsters_to_advance":false,"enemy_count":0,"ladder_has_spawned":false},"status":"available","source":{"kind":"game_object","path":"MineShaft.mustKillAllMonstersToAdvance"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_resources": {"value":{"health":HEALTH,"max_health":100,"energy":ENERGY,"max_energy":270,"mining_level":5,"combat_level":4,"current_time":1200,"deepest_mine_level":DEEPEST_MINE_LEVEL,"staircase_count":2,"selected_slot_index":1,"food_slots":[]},"status":"available","source":{"kind":"game_object","path":"Game1.player"},"adapter":"test","read_at_tick":1,"confidence":1},
            "completeness": {"value":{"status":"complete","unavailable_reasons":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("CURRENT_DEPTH", currentDepth.ToString()).Replace("TARGET_FAMILY", targetFamily).Replace("COLLISION_STATUS", collisionStatus).Replace("HEALTH", health.ToString()).Replace("ENERGY", energy.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("OBJECTS_STATUS", objectsStatus).Replace("DEEPEST_MINE_LEVEL", deepestMineLevel.ToString()));
    }

    private static SmallModelActionEnvelope Request(string stateHash)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.mining.test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.mining.test",
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
                    ActionId = "action.mining.test",
                    OptionId = "mining.reach_depth",
                    Rationale = "test"
                }
            }
        };
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-13T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
