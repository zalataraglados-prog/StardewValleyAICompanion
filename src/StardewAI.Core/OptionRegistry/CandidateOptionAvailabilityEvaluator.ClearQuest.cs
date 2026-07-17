using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] ClearObstacleCandidates(SnapshotEnvelope snapshot)
        {
            var candidates = new List<EventCandidate>();
            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
            if (objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in objects.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var x = ReadInt(item, "tile_x");
                    var y = ReadInt(item, "tile_y");
                    var qualifiedId = ReadString(item, "qualified_item_id");
                    var clearKind = ClearableObjectKind(qualifiedId, ReadString(item, "name"));
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        continue;
                    }

                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, qualifiedId));
                }
            }

            var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            if (terrainFeatures.HasValue && terrainFeatures.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in terrainFeatures.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var type = ReadString(feature, "type");
                    var clearKind = ClearableTerrainFeatureKind(type);
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        continue;
                    }

                    var x = ReadInt(feature, "tile_x");
                    var y = ReadInt(feature, "tile_y");
                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, type));
                }
            }

            return candidates
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private EventCandidate ClearObstacleCandidate(SnapshotEnvelope snapshot, string locationId, int playerX, int playerY, int x, int y, string clearKind, string sourceId)
        {
            var energyCost = ClearObstacleEnergyCost(clearKind);
            var blockReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.clear_obstacle",
                Parameters = new[]
                {
                    Parameter("target_tile_x", x.ToString()),
                    Parameter("target_tile_y", y.ToString()),
                    Parameter("max_tool_swings", "8")
                }
            }).ToList();
            var standTile = FindBestStandTile(snapshot, x, y);
            if (standTile is null)
            {
                blockReasons.Add("clear_obstacle_no_adjacent_route_stand_tile");
            }
            var distance = standTile is not null
                ? Math.Abs(playerX - standTile.X) + Math.Abs(playerY - standTile.Y)
                : 0;
            var estimatedTicks = Math.Max(60, distance * 60 + ClearObstacleToolTicks(clearKind));
            var playerEnergy = ReadStateFieldValue(snapshot, "player", "energy");
            if (playerEnergy.HasValue &&
                playerEnergy.Value.ValueKind == JsonValueKind.Number &&
                playerEnergy.Value.TryGetInt32(out var availableEnergy) &&
                energyCost > availableEnergy)
            {
                blockReasons.Add("insufficient_energy_for_clear_obstacle");
            }

            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            if (currentTime > 0 && WouldFinishAfterClock(currentTime, estimatedTicks, 2600))
            {
                blockReasons.Add("clear_obstacle_would_exceed_day_time_budget");
            }

            return new EventCandidate
            {
                CandidateId = "clear:" + locationId + ":" + x + "," + y + ":" + clearKind,
                Kind = "clear_obstacle_tile",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = (standTile is not null ? "move_to_adjacent=" + standTile.X + "," + standTile.Y + ";" : string.Empty) +
                    "current_location.obstacle[" + x + "," + y + "]=clear;clear_kind=" + clearKind + ";source=" + sourceId,
                EstimatedTicks = estimatedTicks,
                EnergyCost = energyCost,
                AvailabilityClass = "always_available",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static int ClearObstacleToolTicks(string clearKind)
        {
            return clearKind switch
            {
                "grass" => 60,
                "weeds" => 60,
                "stone" => 240,
                "twig" => 240,
                "tree" => 600,
                "fruit_tree" => 600,
                _ => 240
            };
        }

        private static int ClearObstacleEnergyCost(string clearKind)
        {
            return clearKind switch
            {
                "grass" => 0,
                "weeds" => 1,
                "stone" => 2,
                "twig" => 2,
                "tree" => 10,
                "fruit_tree" => 10,
                _ => 2
            };
        }

        private static bool WouldFinishAfterClock(int startTime, int estimatedTicks, int latestFinishTime)
        {
            var estimatedMinutes = (int)Math.Ceiling(Math.Max(0, estimatedTicks) / 60.0);
            return AddClockMinutes(startTime, estimatedMinutes) > latestFinishTime;
        }

        private static int AddClockMinutes(int hhmm, int minutes)
        {
            var total = (hhmm / 100 * 60) + (hhmm % 100) + minutes;
            return total / 60 * 100 + total % 60;
        }

        private static string ClearableObjectKind(string qualifiedId, string name)
        {
            if (qualifiedId is "(O)343" or "(O)450")
            {
                return "stone";
            }

            if (qualifiedId is "(O)294" or "(O)295")
            {
                return "twig";
            }

            if (qualifiedId.StartsWith("(O)Weeds", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("weed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "weeds";
            }

            return string.Empty;
        }

        private EventCandidate[] QuestCandidates(SnapshotEnvelope snapshot)
        {
            var activeQuests = ReadStateFieldValue(snapshot, "quests", "active_quests");
            var specialOrders = ReadStateFieldValue(snapshot, "quests", "special_orders");

            var questRefs = activeQuests.HasValue && activeQuests.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<QuestProgressRef[]>(activeQuests.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                : Array.Empty<QuestProgressRef>();

            var orderRefs = specialOrders.HasValue && specialOrders.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(specialOrders.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();

            var ordinaryCandidates = QuestCandidateBuilder.BuildOrdinaryCandidates(questRefs);
            var specialOrderCandidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(orderRefs);

            var candidates = new List<EventCandidate>();
            var locationId = ReadStateFieldString(snapshot, "player", "location_id");

            foreach (var candidate in ordinaryCandidates)
            {
                var blockReasons = new List<string>(candidate.BlockedDiagnostics);
                blockReasons.Add("quest_native_executor_not_implemented");
                candidates.Add(new EventCandidate
                {
                    CandidateId = candidate.CandidateId,
                    Kind = "quest_candidate",
                    Available = false,
                    LocationId = locationId,
                    ExpectedEffect = "quest_candidate_family=" + candidate.Family +
                        ";runtime_type=" + candidate.RuntimeType +
                        ";next_action=" + candidate.NextActionCategory +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetLocation) ? ";target_location=" + candidate.RequiredTargetLocation : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetNpc) ? ";target_npc=" + candidate.RequiredTargetNpc : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredItemId) ? ";item_id=" + candidate.RequiredItemId : string.Empty) +
                        ";target_count=" + candidate.RequiredTargetCount +
                        ";current_count=" + candidate.CurrentProgressCount +
                        ";time=unknown;energy=unknown",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    BlockReasons = blockReasons.ToArray(),
                    Parameters = new[]
                    {
                        Parameter("candidate_family", candidate.Family),
                        Parameter("candidate_runtime_type", candidate.RuntimeType),
                        Parameter("candidate_next_action", candidate.NextActionCategory),
                        Parameter("candidate_provenance", candidate.Provenance),
                        Parameter("candidate_id", candidate.CandidateId),
                        Parameter("quest_id", candidate.QuestId),
                        Parameter("quest_key", candidate.QuestKey),
                        Parameter("required_target_npc", candidate.RequiredTargetNpc),
                        Parameter("required_target_location", candidate.RequiredTargetLocation),
                        Parameter("required_item_id", candidate.RequiredItemId),
                        Parameter("required_target_count", candidate.RequiredTargetCount.ToString()),
                        Parameter("current_progress_count", candidate.CurrentProgressCount.ToString()),
                        Parameter("is_complete", candidate.IsComplete.ToString().ToLowerInvariant()),
                        Parameter("days_remaining", candidate.DaysRemaining.ToString()),
                        Parameter("due_date", candidate.DueDate.ToString()),
                        Parameter("planning_eligible", "true")
                    }
                });
            }

            foreach (var candidate in specialOrderCandidates)
            {
                var blockReasons = new List<string>(candidate.BlockedDiagnostics);
                blockReasons.Add("quest_native_executor_not_implemented");
                candidates.Add(new EventCandidate
                {
                    CandidateId = candidate.CandidateId,
                    Kind = "special_order_candidate",
                    Available = false,
                    LocationId = locationId,
                    ExpectedEffect = "quest_candidate_family=" + candidate.Family +
                        ";runtime_type=" + candidate.RuntimeType +
                        ";next_action=" + candidate.NextActionCategory +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetLocation) ? ";target_location=" + candidate.RequiredTargetLocation : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetNpc) ? ";target_npc=" + candidate.RequiredTargetNpc : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredItemId) ? ";item_id=" + candidate.RequiredItemId : string.Empty) +
                        ";target_count=" + candidate.RequiredTargetCount +
                        ";current_count=" + candidate.CurrentProgressCount +
                        ";time=unknown;energy=unknown",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    BlockReasons = blockReasons.ToArray(),
                    Parameters = new[]
                    {
                        Parameter("candidate_family", candidate.Family),
                        Parameter("candidate_runtime_type", candidate.RuntimeType),
                        Parameter("candidate_next_action", candidate.NextActionCategory),
                        Parameter("candidate_provenance", candidate.Provenance),
                        Parameter("candidate_id", candidate.CandidateId),
                        Parameter("quest_id", candidate.QuestId),
                        Parameter("quest_key", candidate.QuestKey),
                        Parameter("required_target_npc", candidate.RequiredTargetNpc),
                        Parameter("required_target_location", candidate.RequiredTargetLocation),
                        Parameter("required_item_id", candidate.RequiredItemId),
                        Parameter("required_target_count", candidate.RequiredTargetCount.ToString()),
                        Parameter("current_progress_count", candidate.CurrentProgressCount.ToString()),
                        Parameter("is_complete", candidate.IsComplete.ToString().ToLowerInvariant()),
                        Parameter("days_remaining", candidate.DaysRemaining.ToString()),
                        Parameter("due_date", candidate.DueDate.ToString()),
                        Parameter("planning_eligible", "true")
                    }
                });
            }

            return candidates.ToArray();
        }

        private static string ClearableTerrainFeatureKind(string type)
        {
            if (type.EndsWith(".Grass", StringComparison.Ordinal) || type == "Grass")
            {
                return "grass";
            }

            if (type.EndsWith(".Tree", StringComparison.Ordinal) || type == "Tree")
            {
                return "tree";
            }

            if (type.EndsWith(".FruitTree", StringComparison.Ordinal) || type == "FruitTree")
            {
                return "fruit_tree";
            }

            return string.Empty;
        }

    }
}
