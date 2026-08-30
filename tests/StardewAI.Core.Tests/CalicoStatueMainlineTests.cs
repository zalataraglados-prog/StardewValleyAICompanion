using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Mining;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CalicoStatueMainlineTests
{
    [Fact]
    public void ExactSourceSelectorOrderAndAllEighteenEffectsRemainRepresented()
    {
        var seen = new HashSet<int>();
        var currentEffects = new Dictionary<int, int> { [0] = 1, [4] = 1, [10] = 1 };
        for (var seed = 0; seed < 50000; seed++)
        {
            var actual = CalicoStatueEffectModel.SelectEffect(new Random(seed), 0.025d, currentEffects);
            var expected = SourceReplica(new Random(seed), 0.025d, currentEffects);
            Assert.Equal(expected, actual);
            seen.Add(CalicoStatueEffectModel.SelectEffect(new Random(seed), 0d, new Dictionary<int, int>()));
        }

        Assert.Equal(Enumerable.Range(0, 18), CalicoStatueEffectModel.All.Select(row => row.EffectId));
        Assert.Equal(Enumerable.Range(0, 18), seen.OrderBy(value => value));
        Assert.Equal(100, CalicoStatueEffectModel.GetRequired(17).CalicoEggReward);
        Assert.Equal("neutral", CalicoStatueEffectModel.GetRequired(13).StrategyPolarity);
    }

    [Fact]
    public void ExactProjectedEffectFlowsThroughCandidatePlanAndNativeQueue()
    {
        var snapshot = Snapshot("calico-a", gateStatus: "ready", projectedEffectId: 12);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "mining.activate_calico_statue" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("activate_calico_statue", candidate.Kind);
        AssertParameter(candidate.Parameters, "calico_statue_accepted_effect_id", "12");
        AssertParameter(candidate.Parameters, "stand_tile_x", "19");
        AssertParameter(candidate.Parameters, "stand_tile_y", "20");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("activate_calico_statue", planStep.Kind);
        Assert.Contains("never_directly_write_rating_effects_rewards_health_stamina_buff_tile_or_rng", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.activate_calico_statue", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("activate_calico_statue", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "calico_statue_accepted_effect_id", "12");
        AssertParameter(item.NormalizedCommand.Parameters, "calico_statue_expected_effects_after_csv", "4:1,12:1");
    }

    [Theory]
    [InlineData("excluded_not_desert_festival_skull_cavern")]
    [InlineData("complete_current_floor_statue_already_activated")]
    [InlineData("blocked_host_authoritative_seed_projection_required")]
    public void NonReadyStatuesAreExcludedUpstream(string gateStatus)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot("calico-excluded", gateStatus, 12),
            new[] { "mining.activate_calico_statue" },
            includeExecutorCalibrationOptions: true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsAcceptedEffectWhenSeedProjectionChanges()
    {
        var original = Snapshot("calico-a", "ready", 12);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            original, new[] { "mining.activate_calico_statue" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("calico-b", "ready", 13);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("calico_statue_projected_effect_changed_replan_required",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CapabilityAndRuntimeOwnOneHostNativeMutationPath()
    {
        foreach (var optionId in new[] { "mining.activate_calico_statue", "executor.activate_calico_statue" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-309" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-309" }, capability.RuntimeEvidenceIds);
            Assert.True(capability.HostOnly);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(TrainingEligibilityPolicy.IsEligible(
            OptionCapabilityRegistrySource.GetRequired("mining.activate_calico_statue")));
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.activate_calico_statue"));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CalicoStatue.cs"));
        Assert.Contains("active.Mine.checkAction(", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"calicoEggSkullCavernRating\.Value\s*=(?!=)"), runtime);
        Assert.DoesNotContain("calicoStatueEffects.Clear", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("calicoStatueEffects.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"totalCalicoStatuesActivatedToday\s*=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"recentlyActivatedCalicoStatue\.Value\s*=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"calicoStatueSpot\.Value\s*=(?!=)"), runtime);
    }

    private static int SourceReplica(Random random, double averageDailyLuck, IReadOnlyDictionary<int, int> effects)
    {
        static bool Roll(Random rng, double chance) => chance >= 1d || rng.NextDouble() < chance;
        if (Roll(random, 0.51d + averageDailyLuck))
        {
            foreach (var row in new[]
            {
                (0.15d, 10, false), (0.01d, 17, true), (0.05d, 12, true),
                (0.10d, 15, true), (0.20d, 16, true), (0.10d, 14, true), (0.50d, 11, true)
            })
            {
                if (Roll(random, row.Item1) && (row.Item3 || !effects.ContainsKey(row.Item2)))
                    return row.Item2;
            }
            return 13;
        }
        if (Roll(random, 0.20d))
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var invasion = random.Next(4);
                if (!effects.ContainsKey(invasion))
                    return invasion;
            }
        }
        foreach (var row in new[]
        {
            (0.10d, 4, false), (0.10d, 9, false), (0.10d, 5, false),
            (0.10d, 6, false), (0.20d, 7, true), (0.20d, 8, true)
        })
        {
            if (Roll(random, row.Item1) && (row.Item3 || !effects.ContainsKey(row.Item2)))
                return row.Item2;
        }
        return 13;
    }

    private static SnapshotEnvelope Snapshot(string fingerprint, string gateStatus, int projectedEffectId)
    {
        var effect = CalicoStatueEffectModel.GetRequired(projectedEffectId);
        var expectedEffects = projectedEffectId == 4 ? "4:2" : "4:1," + projectedEffectId + ":1";
        var projection = new
        {
            schema_version = "calico_statue.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            gate_status = gateStatus,
            location_id = "UndergroundMine121",
            mine_level = 121,
            mine_area = 121,
            desert_festival_day = 1,
            target_tile_x = 20,
            target_tile_y = 20,
            target_tile_index_before = 284,
            target_tile_index_after = 285,
            stand_tiles = new[]
            {
                new { tile_x = 20, tile_y = 19, available = true },
                new { tile_x = 19, tile_y = 20, available = true }
            },
            total_activated_today_before = 41,
            next_activation_number = 42,
            rating_before = 7,
            expected_rating_after = 8,
            average_daily_luck = 0.025d,
            days_played = 321,
            unique_game_id_half = "12345",
            use_legacy_random = false,
            current_effects_csv = "4:1",
            projected_effect_id = projectedEffectId,
            projected_effect = new
            {
                effect_id = effect.EffectId,
                effect_key = effect.EffectKey,
                strategy_polarity = effect.StrategyPolarity,
                can_stack = effect.CanStack,
                calico_egg_reward = effect.CalicoEggReward,
                exact_effect = effect.ExactEffect
            },
            expected_effects_after_csv = expectedEffects,
            calico_eggs_before = 3,
            health_before = 50,
            max_health = 100,
            stamina_before = 130.5d,
            max_stamina = 270d,
            interaction_kind = "mineshaft_buildings_tile",
            expected_action_type = "CalicoStatue",
            native_contract = "MineShaft_Buildings_284_checkAction_then_recentlyActivatedCalicoStatue_event_then_master_seeded_effect_rating_and_native_side_effect_receipt"
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("UndergroundMine121"),
                tile_x = Field(18),
                tile_y = Field(20)
            },
            mining = new
            {
                current_mine = Field(new { mine_level = 121, mine_kind = "skull_cavern" }),
                calico_statue = Field(projection),
                tiles = Field(Array.Empty<object>())
            },
            menus = new
            {
                active_menu = Field(new { is_open = false, type = "none" })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
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

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
