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
                    var clearKind = ReadString(item, "clear_kind");
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        clearKind = ClearableObjectKind(qualifiedId, ReadString(item, "name"));
                    }
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        continue;
                    }

                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, qualifiedId, item));
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
                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, type, feature));
                }
            }

            return candidates
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private EventCandidate ClearObstacleCandidate(
            SnapshotEnvelope snapshot,
            string locationId,
            int playerX,
            int playerY,
            int x,
            int y,
            string clearKind,
            string sourceId,
            JsonElement? source = null)
        {
            var energyCost = ClearObstacleEnergyCost(clearKind);
            var projectedHits = source.HasValue
                ? NullableReadInt(source.Value, clearKind == "tree" ? "expected_axe_hits_to_clear" : "expected_tool_hits_to_clear")
                : null;
            var maxToolSwings = Math.Max(1, projectedHits ?? 8);
            var probeParameters = new List<SmallModelActionParameter>
            {
                Parameter("target_tile_x", x.ToString()),
                Parameter("target_tile_y", y.ToString()),
                Parameter("max_tool_swings", maxToolSwings.ToString())
            };
            if (source.HasValue)
            {
                var objectClearanceStatus = ReadString(source.Value, "clear_obstacle_executor_status");
                var hasObjectClearanceProjection = !string.IsNullOrWhiteSpace(objectClearanceStatus) &&
                    !string.Equals(objectClearanceStatus, "not_applicable", StringComparison.Ordinal);
                var skillProjectionStatus = ReadString(source.Value, "harvest_experience_projection_status");
                var hasSkillProjection = !string.IsNullOrWhiteSpace(skillProjectionStatus) &&
                    !string.Equals(skillProjectionStatus, "not_applicable", StringComparison.Ordinal);
                var toolSlotIndex = NullableReadInt(source.Value, "tool_slot_index");
                var requiredToolKind = ReadString(source.Value, "required_tool_kind");
                if (hasObjectClearanceProjection && toolSlotIndex.HasValue)
                {
                    probeParameters.Add(Parameter("tool_slot_index", toolSlotIndex.Value.ToString()));
                }
                if (hasObjectClearanceProjection && !string.IsNullOrWhiteSpace(requiredToolKind))
                {
                    probeParameters.Add(Parameter("required_tool_kind", requiredToolKind));
                }
                foreach (var binding in new[]
                {
                    (ParameterName: "skill_experience_skill_id", FieldName: "harvest_experience_skill_id"),
                    (ParameterName: "skill_experience_on_success_min", FieldName: "harvest_experience_on_success_min"),
                    (ParameterName: "skill_experience_on_success_max", FieldName: "harvest_experience_on_success_max"),
                    (ParameterName: "skill_experience_projection_status", FieldName: "harvest_experience_projection_status"),
                    (ParameterName: "clear_output_projection_status", FieldName: "clear_output_projection_status"),
                    (ParameterName: "clear_output_qualified_item_id", FieldName: "clear_output_qualified_item_id"),
                    (ParameterName: "clear_output_quantity_min", FieldName: "clear_output_quantity_min"),
                    (ParameterName: "clear_output_quantity_max", FieldName: "clear_output_quantity_max")
                })
                {
                    if ((binding.ParameterName.StartsWith("skill_experience", StringComparison.Ordinal) && !hasSkillProjection) ||
                        (binding.ParameterName.StartsWith("clear_output", StringComparison.Ordinal) && !hasObjectClearanceProjection))
                    {
                        continue;
                    }
                    var value = ReadString(source.Value, binding.FieldName);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        var numericValue = NullableReadInt(source.Value, binding.FieldName);
                        value = numericValue?.ToString() ?? string.Empty;
                    }
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        probeParameters.Add(Parameter(binding.ParameterName, value));
                    }
                }
            }
            var blockReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.clear_obstacle",
                Parameters = probeParameters.ToArray()
            }).ToList();
            if (clearKind == "tree" && source.HasValue)
            {
                var status = ReadString(source.Value, "tree_clear_executor_status");
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                {
                    blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "tree_clear_projection_unavailable" : status);
                }
            }
            if (source.HasValue)
            {
                var objectStatus = ReadString(source.Value, "clear_obstacle_executor_status");
                if (!string.IsNullOrWhiteSpace(objectStatus) &&
                    !string.Equals(objectStatus, "not_applicable", StringComparison.Ordinal) &&
                    !string.Equals(objectStatus, "ready", StringComparison.Ordinal))
                {
                    blockReasons.Add(objectStatus);
                }
            }
            var standTile = FindBestStandTile(snapshot, x, y);
            if (standTile is null)
            {
                blockReasons.Add("clear_obstacle_no_adjacent_route_stand_tile");
            }
            var distance = standTile is not null
                ? Math.Abs(playerX - standTile.X) + Math.Abs(playerY - standTile.Y)
                : 0;
            var estimatedTicks = Math.Max(60, distance * 60 + ClearObstacleToolTicks(clearKind, maxToolSwings));
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
                    "current_location.obstacle[" + x + "," + y + "]=clear;clear_kind=" + clearKind + ";source=" + sourceId +
                    ";max_tool_swings=" + maxToolSwings + ClearanceToolEffect(source) + SkillExperienceEffect(source),
                EstimatedTicks = estimatedTicks,
                EnergyCost = energyCost,
                AvailabilityClass = "always_available",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static string SkillExperienceEffect(JsonElement? source)
        {
            if (!source.HasValue)
            {
                return string.Empty;
            }

            var skillId = ReadString(source.Value, "harvest_experience_skill_id");
            var minimum = NullableReadInt(source.Value, "harvest_experience_on_success_min");
            var maximum = NullableReadInt(source.Value, "harvest_experience_on_success_max");
            var condition = ReadString(source.Value, "harvest_experience_condition");
            var status = ReadString(source.Value, "harvest_experience_projection_status");
            if (string.Equals(status, "not_applicable", StringComparison.Ordinal))
            {
                return string.Empty;
            }
            return (!string.IsNullOrWhiteSpace(skillId) ? ";skill_experience_skill_id=" + skillId : string.Empty) +
                (minimum.HasValue ? ";skill_experience_on_success_min=" + minimum.Value : string.Empty) +
                (maximum.HasValue ? ";skill_experience_on_success_max=" + maximum.Value : string.Empty) +
                (!string.IsNullOrWhiteSpace(condition) ? ";skill_experience_condition=" + condition : string.Empty) +
                (!string.IsNullOrWhiteSpace(status) ? ";skill_experience_projection_status=" + status : string.Empty);
        }

        private static string ClearanceToolEffect(JsonElement? source)
        {
            if (!source.HasValue)
            {
                return string.Empty;
            }

            var toolSlotIndex = NullableReadInt(source.Value, "tool_slot_index");
            var requiredToolKind = ReadString(source.Value, "required_tool_kind");
            var outputStatus = ReadString(source.Value, "clear_output_projection_status");
            if (string.Equals(outputStatus, "not_applicable", StringComparison.Ordinal))
            {
                return string.Empty;
            }
            var outputQualifiedItemId = ReadString(source.Value, "clear_output_qualified_item_id");
            var outputMinimum = NullableReadInt(source.Value, "clear_output_quantity_min");
            var outputMaximum = NullableReadInt(source.Value, "clear_output_quantity_max");
            return (toolSlotIndex.HasValue ? ";tool_slot_index=" + toolSlotIndex.Value : string.Empty) +
                (!string.IsNullOrWhiteSpace(requiredToolKind) ? ";required_tool_kind=" + requiredToolKind : string.Empty) +
                (!string.IsNullOrWhiteSpace(outputStatus) ? ";clear_output_projection_status=" + outputStatus : string.Empty) +
                (!string.IsNullOrWhiteSpace(outputQualifiedItemId) ? ";clear_output_qualified_item_id=" + outputQualifiedItemId : string.Empty) +
                (outputMinimum.HasValue ? ";clear_output_quantity_min=" + outputMinimum.Value : string.Empty) +
                (outputMaximum.HasValue ? ";clear_output_quantity_max=" + outputMaximum.Value : string.Empty);
        }

        private static int ClearObstacleToolTicks(string clearKind, int maxToolSwings)
        {
            return clearKind switch
            {
                "grass" => 60,
                "weeds" => 60,
                "stone" => 240,
                "twig" => 240,
                "artifact_spot" => 60,
                "tree" => Math.Max(1, maxToolSwings) * 60,
                "fruit_tree" => Math.Max(1, maxToolSwings) * 60,
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
                "artifact_spot" => 2,
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

            if (qualifiedId is "(O)590" or "(O)SeedSpot")
            {
                return "artifact_spot";
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
