using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class FishingTerminalProbabilitySnapshotProjectorTests
{
    [Fact]
    public void ProjectsHashableForecastContextIntoConservativeProbability()
    {
        using var document = JsonDocument.Parse(SnapshotJson(
            Rule("competitor", "168", 0, 0.2d, 1d),
            Rule("target", "145", 1, 0.5d, 0.4d)));

        var result = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            1,
            5);

        Assert.True(result.Resolved);
        Assert.Equal("resolved_fishing_terminal_probability_projection", result.Status);
        Assert.Equal((5, 5, 4),
            (result.BobberTileX, result.BobberTileY, result.WaterDepth));
        Assert.Equal((5, 5),
            (result.EffectiveFishingLevel, result.BaseFishingLevel));
        Assert.NotNull(result.Probability);
        Assert.Equal(0.16d,
            result.Probability!.SingleAttemptProbabilityLowerBound!.Value,
            10);
        Assert.True(result.Probability.IndependentRetryLowerBoundProven);
    }

    [Fact]
    public void RandomSelectorProbabilityUsesExactUniformNativeChoice()
    {
        using var document = JsonDocument.Parse(SnapshotJson(
            Rule(
                "random-target",
                "145",
                0,
                0.5d,
                0.4d,
                randomSelectors: new[] { "145", "168" })));

        var result = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            1,
            5);

        Assert.True(result.Resolved);
        Assert.Equal(0.1d,
            result.Probability!.SingleAttemptProbabilityLowerBound!.Value,
            10);
    }

    [Fact]
    public void UnseededRandomTargetConditionBlocksProbabilityEvidence()
    {
        using var document = JsonDocument.Parse(SnapshotJson(
            Rule("target", "145", 0, 0.5d, 0.4d,
                conditionResolved: false)));

        var result = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            1,
            5);

        Assert.False(result.Resolved);
        Assert.Contains(
            "target_rule_eligibility_unresolved:target",
            result.BlockingReasons);
    }

    [Fact]
    public void RejectsWrongRequestIdentityAndImpossibleCastGeometry()
    {
        using var document = JsonDocument.Parse(SnapshotJson(
            Rule("target", "145", 0, 0.5d, 0.4d)));

        var wrongLocation = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Forest",
            2,
            "(O)145",
            0,
            1,
            5);
        var diagonalCast = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            4,
            4);

        Assert.Contains(
            "fishing_forecast_request_identity_mismatch",
            wrongLocation.BlockingReasons);
        Assert.Contains(
            "fishing_forecast_terminal_cast_geometry_invalid",
            diagonalCast.BlockingReasons);
    }

    [Fact]
    public void AttachedBaitKeepsFirstAttemptButBlocksIndependentRetryProof()
    {
        using var document = JsonDocument.Parse(SnapshotJsonWithBait(
            new { qualified_item_id = "(O)685", stack = 5 },
            5,
            Rule("target", "145", 0, 0.5d, 0.4d)));

        var result = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            1,
            5);

        Assert.True(result.Resolved);
        Assert.Equal(0.2d,
            result.Probability!.SingleAttemptProbabilityLowerBound!.Value,
            10);
        Assert.False(result.Probability.IndependentRetryLowerBoundProven);
    }

    [Fact]
    public void TemporaryFishingLevelBuffBlocksIndependentRetryProof()
    {
        using var document = JsonDocument.Parse(SnapshotJsonWithBait(
            null,
            4,
            Rule("target", "145", 0, 0.5d, 0.4d)));

        var result = FishingTerminalProbabilitySnapshotProjector.Project(
            document.RootElement,
            "Beach",
            2,
            "(O)145",
            0,
            1,
            5);

        Assert.True(result.Resolved);
        Assert.False(result.Probability!.IndependentRetryLowerBoundProven);
    }

    private static string SnapshotJson(params object[] rules) =>
        SnapshotJsonWithBait(null, 5, rules);

    private static string SnapshotJsonWithBait(
        object? selectedBait,
        int baseFishingLevel,
        params object[] rules)
    {
        var fieldSource = new { kind = "game_object", path = "test" };
        object Field(object value) => new
        {
            value,
            status = "available",
            source = fieldSource,
            adapter = "test",
            read_at_tick = 42,
            confidence = 1
        };

        return JsonSerializer.Serialize(new
        {
            state = new
            {
                fishing = new
                {
                    forecast_request = Field(new
                    {
                        profile = "fishing_forecast",
                        target_location_id = "Beach",
                        rod_slot_index = 2,
                        request_complete = true
                    }),
                    location_context = Field(new
                    {
                        location_id = "Beach",
                        rod_slot_index = 2,
                        can_fish_here = true
                    }),
                    fishable_tiles = Field(new[]
                    {
                        new { tile_x = 5, tile_y = 5, water_depth = 4 }
                    }),
                    spawn_rules = Field(new
                    {
                        inventory_complete = true,
                        evaluation_context = new
                        {
                            fishing_level = 5,
                            base_fishing_level = baseFishingLevel,
                            selected_bait_qualified_item_id = selectedBait is null
                                ? null
                                : "(O)685",
                            has_magic_bait = false,
                            has_curiosity_lure = false
                        },
                        rules
                    })
                }
            }
        });
    }

    private static object Rule(
        string key,
        string itemId,
        int precedence,
        double spawnChance,
        double acceptanceChance,
        bool conditionResolved = true,
        string[]? randomSelectors = null)
    {
        var selectors = randomSelectors ?? Array.Empty<string>();
        var random = selectors.Length > 0;
        var outputs = (random ? selectors : new[] { itemId })
            .Select((selector, index) => new
            {
                output_index = index,
                qualified_item_id = "(O)" + selector,
                resolution_complete = true,
                output_eligible_before_random_rolls = true,
                data_fish_chance_roll_pending = true,
                data_fish_chance_probability_resolved = true,
                data_fish_chance_by_water_depth = new[]
                {
                    new
                    {
                        water_depth = 4,
                        chance_preview = acceptanceChance
                    }
                }
            })
            .ToArray();
        return new
        {
            rule_key = key,
            precedence,
            item_id = random ? null : itemId,
            random_item_ids = selectors,
            item_selection_mode = random ? "random_item_id" : "item_id",
            per_item_condition = (string?)null,
            condition = (string?)null,
            condition_probability_resolved = conditionResolved,
            condition_met_for_probability = conditionResolved ? true : (bool?)null,
            player_position = (object?)null,
            min_fishing_level = 0,
            blocking_reasons = Array.Empty<string>(),
            eligible_fishable_tile_indices = new[] { 0 },
            spawn_chance_probability_resolved = true,
            use_fish_caught_seeded_random = false,
            catch_limit = -1,
            set_flag_on_catch = (string?)null,
            effective_spawn_chance_preview = spawnChance,
            outputs
        };
    }
}
