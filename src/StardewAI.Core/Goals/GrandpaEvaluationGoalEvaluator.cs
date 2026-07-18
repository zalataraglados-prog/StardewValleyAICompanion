using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Goals
{
    public sealed class GrandpaEvaluationGoalEvaluator
    {
        public static readonly string[] RequiredFactPaths =
        {
            "player.total_money_earned",
            "player.has_skull_key",
            "player.has_rusty_key",
            "player.married_or_roommate",
            "player.farmhouse_upgrade_level",
            "player.level",
            "world_progress.achievements",
            "world_progress.community_center",
            "npcs.friendships",
            "quests.mail_received",
            "game.year",
            "farm.grandpa_score",
            "player.active_object_qualified_id"
        };

        public GrandpaEvaluationGoalReport Evaluate(WorldModelEnvelope model)
        {
            var factors = new List<GrandpaEvaluationFactor>();
            AddMoneyFactors(model, factors);
            AddAchievementFactor(model, factors, "achievement_complete_collection", "Achievement 5", 5);
            AddBooleanFactor(model, factors, "skull_key", "Skull Key", "player.has_skull_key", true, "Game1.player.hasSkullKey");
            AddCommunityCenterFactors(model, factors);
            AddMarriageHouseFactor(model, factors);
            AddBooleanFactor(model, factors, "rusty_key", "Rusty Key", "player.has_rusty_key", true, "Game1.player.hasRustyKey");
            AddAchievementFactor(model, factors, "achievement_master_angler", "Achievement 26", 26);
            AddAchievementFactor(model, factors, "achievement_full_shipment", "Achievement 34", 34);
            AddFriendshipFactors(model, factors);
            AddLevelFactors(model, factors);
            AddPetLoveFactor(model, factors);

            var currentScore = factors.Where(factor => factor.Known).Sum(factor => factor.Points);
            var missingFacts = RequiredFactPaths
                .Where(path => !IsKnown(model, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            return new GrandpaEvaluationGoalReport
            {
                CurrentScore = currentScore,
                CurrentCandles = CandlesFromScore(currentScore),
                FourCandleMilestoneMet = currentScore >= GrandpaEvaluationGoalDefinition.FourCandleScore,
                PointsNeeded = Math.Max(0, GrandpaEvaluationGoalDefinition.MaximumRuleScore - currentScore),
                TargetMet = currentScore >= GrandpaEvaluationGoalDefinition.MaximumRuleScore,
                RequiredFactPaths = RequiredFactPaths,
                MissingFactPaths = missingFacts,
                Factors = factors.ToArray(),
                EvaluationContext = BuildContext(model)
            };
        }

        private static GrandpaEvaluationContext BuildContext(WorldModelEnvelope model)
        {
            var year = ReadInt(model.Facts.Game, "year");
            var recordedCandles = ReadInt(model.Facts.Farm, "grandpa_score");
            var activeObject = ReadString(model.Facts.Player, "active_object_qualified_id") ?? string.Empty;
            bool? initialEvaluationAvailable = year.HasValue ? year.Value >= 3 : null;
            bool? reevaluationAvailable = year.HasValue && recordedCandles.HasValue
                ? year.Value >= 3 && recordedCandles.Value > 0 && recordedCandles.Value < 4
                : null;
            var holdingDiamond = string.Equals(activeObject, "(O)72", StringComparison.Ordinal);

            return new GrandpaEvaluationContext
            {
                Year = year,
                InitialEvaluationAvailable = initialEvaluationAvailable,
                RecordedGrandpaCandles = recordedCandles,
                ReevaluationAvailable = reevaluationAvailable,
                ActiveObjectQualifiedId = activeObject,
                HoldingReevaluationItem = string.IsNullOrEmpty(activeObject) ? null : holdingDiamond,
                Notes = new[]
                {
                    "Initial/re-evaluation availability is planning context, not a score input.",
                    "Post-year-3 re-evaluation requires year >= 3, recorded candles between 1 and 3, and diamond active object (O)72."
                }
            };
        }

        private static void AddMoneyFactors(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var money = ReadUInt(model.Facts.Player, "total_money_earned");
            AddThreshold(factors, "money_50000", "Total money earned >= 50,000", "player.total_money_earned", money, 50000, 1, "Game1.player.totalMoneyEarned >= 50000");
            AddThreshold(factors, "money_100000", "Total money earned >= 100,000", "player.total_money_earned", money, 100000, 1, "Game1.player.totalMoneyEarned >= 100000");
            AddThreshold(factors, "money_200000", "Total money earned >= 200,000", "player.total_money_earned", money, 200000, 1, "Game1.player.totalMoneyEarned >= 200000");
            AddThreshold(factors, "money_300000", "Total money earned >= 300,000", "player.total_money_earned", money, 300000, 1, "Game1.player.totalMoneyEarned >= 300000");
            AddThreshold(factors, "money_500000", "Total money earned >= 500,000", "player.total_money_earned", money, 500000, 1, "Game1.player.totalMoneyEarned >= 500000");
            AddThreshold(factors, "money_1000000", "Total money earned >= 1,000,000", "player.total_money_earned", money, 1000000, 2, "Game1.player.totalMoneyEarned >= 1000000");
        }

        private static void AddCommunityCenterFactors(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var communityCenter = ReadObject(model.Facts.WorldProgress, "community_center");
            var accessible = ReadBool(communityCenter, "location_accessible");
            var completed = ReadBool(communityCenter, "completed");
            var known = accessible.HasValue || completed.HasValue;
            var firstSatisfied = accessible == true || completed == true;

            factors.Add(Factor(
                "community_center_access_or_completion",
                "Community Center accessible or completed",
                "world_progress.community_center",
                known,
                firstSatisfied ? 1 : 0,
                1,
                firstSatisfied,
                Current(accessible, completed),
                "accessible=true or completed=true",
                "Game1.isLocationAccessible(\"CommunityCenter\") || Game1.player.hasCompletedCommunityCenter()"));

            factors.Add(Factor(
                "community_center_accessible_bonus",
                "Community Center accessible bonus",
                "world_progress.community_center",
                accessible.HasValue,
                accessible == true ? 2 : 0,
                2,
                accessible == true,
                accessible.HasValue ? accessible.Value.ToString() : string.Empty,
                "accessible=true",
                "Game1.isLocationAccessible(\"CommunityCenter\")"));
        }

        private static void AddMarriageHouseFactor(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var married = ReadBool(model.Facts.Player, "married_or_roommate");
            var houseLevel = ReadInt(model.Facts.Player, "farmhouse_upgrade_level");
            var known = married.HasValue && houseLevel.HasValue;
            var satisfied = married == true && houseLevel >= 2;

            factors.Add(Factor(
                "married_or_roommate_house_2",
                "Married or roommate and farmhouse upgrade >= 2",
                "player.married_or_roommate,player.farmhouse_upgrade_level",
                known,
                satisfied ? 1 : 0,
                1,
                satisfied,
                known ? $"married_or_roommate={married}; farmhouse_upgrade_level={houseLevel}" : string.Empty,
                "married_or_roommate=true and farmhouse_upgrade_level>=2",
                "Game1.player.isMarriedOrRoommates() && Utility.getHomeOfFarmer(Game1.player).upgradeLevel >= 2"));
        }

        private static void AddFriendshipFactors(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var count = CountFriendsAtOrAbove(model, 1975);
            AddCountThreshold(factors, "friendships_5", "At least 5 friends with >= 1975 friendship points", "npcs.friendships", count, 5, 1, "Utility.getNumberOfFriendsWithinThisRange(Game1.player, 1975, 999999) >= 5");
            AddCountThreshold(factors, "friendships_10", "At least 10 friends with >= 1975 friendship points", "npcs.friendships", count, 10, 1, "Utility.getNumberOfFriendsWithinThisRange(Game1.player, 1975, 999999) >= 10");
        }

        private static void AddLevelFactors(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var level = ReadInt(model.Facts.Player, "level");
            AddThreshold(factors, "player_level_15", "Total player level >= 15", "player.level", level, 15, 1, "Game1.player.Level >= 15");
            AddThreshold(factors, "player_level_25", "Total player level >= 25", "player.level", level, 25, 1, "Game1.player.Level >= 25");
        }

        private static void AddPetLoveFactor(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors)
        {
            var mail = ReadStringArray(model.Facts.Quests, "mail_received");
            var known = mail is not null;
            var satisfied = mail?.Contains("petLoveMessage", StringComparer.Ordinal) == true;
            factors.Add(Factor("pet_love", "Pet love message received", "quests.mail_received", known, satisfied ? 1 : 0, 1, satisfied, known ? string.Join(",", mail!) : string.Empty, "mail contains petLoveMessage", "Game1.player.mailReceived.Contains(\"petLoveMessage\")"));
        }

        private static void AddAchievementFactor(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors, string id, string label, int achievementId)
        {
            var achievements = ReadIntArray(model.Facts.WorldProgress, "achievements");
            var known = achievements is not null;
            var satisfied = achievements?.Contains(achievementId) == true;
            factors.Add(Factor(id, label, "world_progress.achievements", known, satisfied ? 1 : 0, 1, satisfied, known ? string.Join(",", achievements!) : string.Empty, achievementId.ToString(), $"Game1.player.achievements.Contains({achievementId})"));
        }

        private static void AddBooleanFactor(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors, string id, string label, string path, bool target, string sourceRule)
        {
            var value = ReadBool(model.Facts.Player, path.Substring("player.".Length));
            var satisfied = value == target;
            factors.Add(Factor(id, label, path, value.HasValue, satisfied ? 1 : 0, 1, satisfied, value.HasValue ? value.Value.ToString() : string.Empty, target.ToString(), sourceRule));
        }

        private static void AddWorldProgressBooleanFactor(WorldModelEnvelope model, List<GrandpaEvaluationFactor> factors, string id, string label, string path, bool target, string sourceRule)
        {
            var value = ReadBool(model.Facts.WorldProgress, path.Substring("world_progress.".Length));
            var satisfied = value == target;
            factors.Add(Factor(id, label, path, value.HasValue, satisfied ? 1 : 0, 1, satisfied, value.HasValue ? value.Value.ToString() : string.Empty, target.ToString(), sourceRule));
        }

        private static void AddThreshold(List<GrandpaEvaluationFactor> factors, string id, string label, string path, long? value, long threshold, int maxPoints, string sourceRule)
        {
            var satisfied = value >= threshold;
            factors.Add(Factor(id, label, path, value.HasValue, satisfied ? maxPoints : 0, maxPoints, satisfied, value?.ToString() ?? string.Empty, threshold.ToString(), sourceRule));
        }

        private static void AddCountThreshold(List<GrandpaEvaluationFactor> factors, string id, string label, string path, int? value, int threshold, int maxPoints, string sourceRule)
        {
            var satisfied = value >= threshold;
            factors.Add(Factor(id, label, path, value.HasValue, satisfied ? maxPoints : 0, maxPoints, satisfied, value?.ToString() ?? string.Empty, threshold.ToString(), sourceRule));
        }

        private static GrandpaEvaluationFactor Factor(string id, string label, string path, bool known, int points, int maxPoints, bool satisfied, string current, string target, string sourceRule)
        {
            return new GrandpaEvaluationFactor
            {
                Id = id,
                Label = label,
                FactPath = path,
                Known = known,
                Points = known ? points : 0,
                MaxPoints = maxPoints,
                Satisfied = known && satisfied,
                CurrentValue = current,
                TargetValue = target,
                SourceRule = sourceRule
            };
        }

        private static int CandlesFromScore(int score)
        {
            if (score >= 12)
            {
                return 4;
            }
            if (score >= 8)
            {
                return 3;
            }
            if (score >= 4)
            {
                return 2;
            }
            return 1;
        }

        private static bool IsKnown(WorldModelEnvelope model, string path)
        {
            var parts = path.Split('.');
            var section = parts[0] switch
            {
                "game" => model.Facts.Game,
                "player" => model.Facts.Player,
                "farm" => model.Facts.Farm,
                "world_progress" => model.Facts.WorldProgress,
                "npcs" => model.Facts.Npcs,
                "quests" => model.Facts.Quests,
                _ => null
            };

            return section is not null && parts.Length == 2 && section.ContainsKey(parts[1]);
        }

        private static int? CountFriendsAtOrAbove(WorldModelEnvelope model, int points)
        {
            if (!model.Facts.Npcs.TryGetValue("friendships", out var friendships) || friendships.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var count = 0;
            foreach (var friendship in friendships.EnumerateArray())
            {
                if (friendship.TryGetProperty("points", out var value) && value.TryGetInt32(out var current) && current >= points)
                {
                    count++;
                }
            }

            return count;
        }

        private static JsonElement? ReadObject(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;
        }

        private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) ? ReadBool(value) : null;
        }

        private static bool? ReadBool(JsonElement? source, string key)
        {
            if (!source.HasValue || source.Value.ValueKind != JsonValueKind.Object || !source.Value.TryGetProperty(key, out var value))
            {
                return null;
            }
            return ReadBool(value);
        }

        private static bool? ReadBool(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static int? ReadInt(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : null;
        }

        private static uint? ReadUInt(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.TryGetUInt32(out var result) ? result : null;
        }

        private static string? ReadString(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int[]? ReadIntArray(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(item => item.GetInt32()).ToArray()
                : null;
        }

        private static string[]? ReadStringArray(IReadOnlyDictionary<string, JsonElement> section, string key)
        {
            return section.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                : null;
        }

        private static string Current(bool? accessible, bool? completed)
        {
            return accessible.HasValue || completed.HasValue
                ? $"accessible={accessible?.ToString() ?? "unknown"}; completed={completed?.ToString() ?? "unknown"}"
                : string.Empty;
        }
    }
}
