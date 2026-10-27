using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.OptionRegistry
{
    internal static partial class FishingEventCandidateBuilder
    {
        private static void AddNormalRuleCandidates(
            JsonElement rodContext,
            JsonElement spawnRules,
            JsonElement rules,
            FishableTile[] tiles,
            HashSet<int> reservedTileIndices,
            int fishingLevel,
            CollisionGrid grid,
            IReadOnlyDictionary<string, int> routeDistances,
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            ICollection<EventCandidate> candidates)
        {
            var normalTiles = tiles
                .Where(tile => !reservedTileIndices.Contains(tile.Index))
                .ToArray();
            var orderedRules = rules.EnumerateArray().ToArray();
            foreach (var cast in RepresentativeNormalCasts(
                orderedRules,
                normalTiles,
                fishingLevel,
                grid,
                routeDistances))
            {
                foreach (var rule in orderedRules)
                {
                    AddRuleOutcomesForCast(
                        rodContext,
                        orderedRules,
                        rule,
                        cast,
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        candidates);
                }

                if (!CastHasGuaranteedTerminalRule(orderedRules, cast))
                {
                    AddBaseFallbackCandidate(
                        rodContext,
                        spawnRules,
                        cast,
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        candidates);
                }
            }
        }

        private static CastSelection[] RepresentativeNormalCasts(
            JsonElement[] rules,
            FishableTile[] normalTiles,
            int fishingLevel,
            CollisionGrid grid,
            IReadOnlyDictionary<string, int> routeDistances)
        {
            var casts = new Dictionary<string, CastSelection>(
                StringComparer.Ordinal);
            Add(FindBestCast(
                normalTiles,
                default,
                fishingLevel,
                grid,
                routeDistances));

            foreach (var rule in rules)
            {
                if (!RuleCanProduceEligibleOutput(rule))
                    continue;

                var eligibleIndices = Ints(
                    rule,
                    "eligible_fishable_tile_indices");
                var playerRectangle = rule.TryGetProperty(
                    "player_position",
                    out var rectangle)
                        ? rectangle
                        : default;
                Add(FindBestCast(
                    normalTiles.Where(tile =>
                        eligibleIndices.Contains(tile.Index)),
                    playerRectangle,
                    fishingLevel,
                    grid,
                    routeDistances,
                    cast => !RuleIsHardShadowed(rules, rule, cast)));
            }

            return casts.Values
                .OrderBy(cast => cast.RouteDistance)
                .ThenByDescending(cast => cast.MaxCastRequested)
                .ThenByDescending(cast => cast.Bobber.WaterDepth)
                .ThenBy(cast => cast.StandY)
                .ThenBy(cast => cast.StandX)
                .ThenBy(cast => cast.Bobber.Y)
                .ThenBy(cast => cast.Bobber.X)
                .ToArray();

            void Add(CastSelection? cast)
            {
                if (cast is null)
                    return;
                var key = string.Join("|", new[]
                {
                    cast.StandX.ToString(),
                    cast.StandY.ToString(),
                    cast.Bobber.X.ToString(),
                    cast.Bobber.Y.ToString(),
                    cast.Direction.ToString(),
                    cast.Distance.ToString()
                });
                casts.TryAdd(key, cast);
            }
        }

        private static bool RuleCanProduceEligibleOutput(JsonElement rule)
        {
            var fixedRuleBlocks = Strings(rule, "blocking_reasons")
                .Where(reason => reason != "player_position_mismatch")
                .ToArray();
            return fixedRuleBlocks.Length == 0 &&
                Bool(rule, "condition_met") == true &&
                rule.TryGetProperty("outputs", out var outputs) &&
                outputs.ValueKind == JsonValueKind.Array &&
                outputs.EnumerateArray().Any(OutputIsEligible);
        }

        private static void AddRuleOutcomesForCast(
            JsonElement rodContext,
            JsonElement[] rules,
            JsonElement rule,
            CastSelection cast,
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            ICollection<EventCandidate> candidates)
        {
            var eligibleIndices = Ints(
                rule,
                "eligible_fishable_tile_indices");
            var playerRectangle = rule.TryGetProperty(
                "player_position",
                out var rectangle)
                    ? rectangle
                    : default;
            if (!RuleCanProduceEligibleOutput(rule) ||
                !eligibleIndices.Contains(cast.Bobber.Index) ||
                !RectangleContains(
                    playerRectangle,
                    cast.StandX,
                    cast.StandY) ||
                RuleIsHardShadowed(rules, rule, cast))
            {
                return;
            }

            var outputs = rule.GetProperty("outputs");
            foreach (var output in outputs.EnumerateArray()
                .Where(OutputIsEligible))
            {
                var chanceFactors = new[]
                    {
                        Double(rule, "effective_spawn_chance_preview"),
                        ChanceAtDepth(output, cast.Bobber.WaterDepth),
                        Double(output, "output_local_chance_preview")
                    }
                    .Where(chance => chance.HasValue)
                    .Select(chance => chance!.Value)
                    .ToArray();
                double? expectedChance = chanceFactors.Length > 0
                    ? chanceFactors.Aggregate(
                        1d,
                        (product, chance) => product * chance)
                    : null;
                var fallbackMultiplier = BaseCatchFallbackMultiplier(
                    rodContext,
                    cast.Bobber.WaterDepth);
                if (fallbackMultiplier <= 0d)
                    continue;
                if (expectedChance.HasValue)
                    expectedChance *= fallbackMultiplier;

                candidates.Add(OutcomeCandidate(
                    locationId,
                    rodSlot,
                    rodQualifiedId,
                    energyCost,
                    "rule",
                    String(rule, "rule_key"),
                    Int(output, "output_index"),
                    String(output, "item_id"),
                    String(output, "qualified_item_id"),
                    cast,
                    expectedChance,
                    expectedChance.HasValue
                        ? "rule_local_preview"
                        : "unresolved_rule_local_probability",
                    IntNullable(output, "effective_fish_difficulty"),
                    Bool(rule, "is_boss_fish") == true,
                    MaximumRawFishQuality(rodContext),
                    Strings(output, "context_tags"),
                    String(output, "context_tags_projection_status")));
            }
        }

        private static bool RuleIsHardShadowed(
            IEnumerable<JsonElement> rules,
            JsonElement targetRule,
            CastSelection cast)
        {
            var targetPrecedence = IntNullable(targetRule, "precedence");
            if (!targetPrecedence.HasValue)
                return false;

            return rules.Any(rule =>
            {
                var precedence = IntNullable(rule, "precedence");
                return precedence.HasValue &&
                    precedence.Value < targetPrecedence.Value &&
                    RuleAppliesToCast(rule, cast) &&
                    RuleIsGuaranteedTerminal(rule);
            });
        }

        private static bool CastHasGuaranteedTerminalRule(
            IEnumerable<JsonElement> rules,
            CastSelection cast) => rules.Any(rule =>
                RuleAppliesToCast(rule, cast) &&
                RuleIsGuaranteedTerminal(rule));

        private static bool RuleAppliesToCast(
            JsonElement rule,
            CastSelection cast)
        {
            if (!RuleCanProduceEligibleOutput(rule) ||
                !Ints(rule, "eligible_fishable_tile_indices")
                    .Contains(cast.Bobber.Index))
            {
                return false;
            }

            var playerRectangle = rule.TryGetProperty(
                "player_position",
                out var rectangle)
                    ? rectangle
                    : default;
            return RectangleContains(
                playerRectangle,
                cast.StandX,
                cast.StandY);
        }

        private static bool RuleIsGuaranteedTerminal(JsonElement rule)
        {
            if (Bool(rule, "spawn_chance_probability_resolved") != true ||
                Double(rule, "effective_spawn_chance_preview") is not { } chance ||
                chance < 1d ||
                !rule.TryGetProperty("outputs", out var outputs) ||
                outputs.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var projectedOutputs = outputs.EnumerateArray().ToArray();
            return projectedOutputs.Length > 0 &&
                projectedOutputs.All(output =>
                    Bool(output, "resolution_complete") == true &&
                    Bool(output, "output_eligible_before_random_rolls") == true &&
                    Strings(output, "output_blocking_reasons").Length == 0 &&
                    Bool(output, "data_fish_chance_probability_resolved") == true &&
                    Bool(output, "data_fish_chance_roll_pending") == false);
        }

        private static bool OutputIsEligible(JsonElement output)
        {
            var resolutionStatus = String(output, "resolution_status");
            return Bool(output, "resolution_complete") == true &&
                Bool(output, "output_eligible_before_random_rolls") == true &&
                resolutionStatus is
                    "direct_item" or "vanilla_secret_note_or_item";
        }
    }
}
