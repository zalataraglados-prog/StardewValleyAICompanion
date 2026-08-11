using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static SmallModelActionParameter[] BuildNormalizedParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            return ActionParameterCompilers.TryGetValue(action.OptionId, out var compiler)
                ? compiler(action, snapshot)
                : action.Parameters;
        }

        private static SmallModelActionParameter[] BuildSocialParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("compiler_context.social_native_executor", "implemented"),
                Parameter("compiler_context.duration", "unknown_until_route_and_native_executor_timing"),
                Parameter("compiler_context.future_schedule_candidates", "not_emitted")
            };
            var candidate = SocialCandidateBuilder.FindMatching(snapshot, action);
            if (candidate is not null)
            {
                parameters.AddRange(candidate.Parameters.Select(parameter => Parameter("candidate." + parameter.Name, parameter.Value)));
                parameters.Add(Parameter("candidate.id", candidate.CandidateId));
                parameters.Add(Parameter("candidate.expected_effect", candidate.ExpectedEffect));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildSelectSafeItemSlotParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters);
            if (ReadIntParameter(action, "safe_slot_index").HasValue)
            {
                return parameters.ToArray();
            }

            var safeSlot = SafeSlotIndex(snapshot);
            if (safeSlot.HasValue)
            {
                parameters.Add(Parameter("safe_slot_index", safeSlot.Value.ToString()));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildCloseMenuParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var activeMenuType = ActiveMenuType(snapshot);
            var incubatorBirthMessage =
                Infrastructure.IncubatorSnapshotProjection
                    .IsBirthMessage(snapshot);
            var closeMenuExecutor = activeMenuType switch
            {
                "LevelUpMenu" => "LevelUpMenu native completion path",
                "ShippingMenu" => "ShippingMenu native OK-button completion path",
                "LetterViewerMenu" => "LetterViewerMenu native page, attachment, quest and close-button input path",
                "DialogueBox" when incubatorBirthMessage =>
                    "incubator birth message native input path",
                _ => "Game1.exitActiveMenu"
            };
            var parameters = new List<SmallModelActionParameter>(
                action.Parameters.Where(parameter =>
                    !string.Equals(
                        parameter.Name,
                        "interaction_kind",
                        StringComparison.Ordinal)))
            {
                Parameter("compiler_context.active_menu_open", ActiveMenuOpen(snapshot).ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_type", activeMenuType),
                Parameter("compiler_context.close_menu_executor", closeMenuExecutor)
            };
            if (incubatorBirthMessage)
            {
                parameters.Add(Parameter(
                    "interaction_kind",
                    "incubator_birth_message"));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildBuyShopItemParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters);
            if (ReadIntParameter(action, "quantity") is null)
            {
                parameters.Add(Parameter("quantity", "1"));
            }

            var existingQualifiedId = ReadParameter(action, "qualified_item_id");
            if (!string.IsNullOrWhiteSpace(existingQualifiedId))
            {
                var existingCandidate = FindPurchaseCandidate(snapshot, existingQualifiedId, ReadParameter(action, "shop_item_id"));
                if (existingCandidate.HasValue)
                {
                    if (string.IsNullOrWhiteSpace(ReadParameter(action, "shop_item_id")))
                    {
                        parameters.Add(Parameter("shop_item_id", ReadString(existingCandidate.Value, "item_id")));
                    }

                    if (ReadIntParameter(action, "max_unit_price") is null)
                    {
                        parameters.Add(Parameter("max_unit_price", ReadInt(existingCandidate.Value, "price").ToString()));
                    }

                    if (string.IsNullOrWhiteSpace(ReadParameter(action, "expected_shop_id")))
                    {
                        var stockForExisting = ReadStateFieldValue(snapshot, "menus", "shop_stock");
                        if (stockForExisting.HasValue && stockForExisting.Value.ValueKind == JsonValueKind.Object)
                        {
                            parameters.Add(Parameter("expected_shop_id", ReadString(stockForExisting.Value, "shop_id")));
                        }
                    }
                }
                else if (HasRuntimeShopStockRecheckParameters(action) &&
                    !parameters.Any(parameter => parameter.Name == "compiler_context.runtime_shop_stock_recheck_required"))
                {
                    parameters.Add(Parameter("compiler_context.runtime_shop_stock_recheck_required", "true"));
                }

                return parameters.ToArray();
            }

            var candidate = FirstSafePurchaseCandidate(snapshot);
            if (candidate is null)
            {
                return parameters.ToArray();
            }

            parameters.Add(Parameter("qualified_item_id", ReadString(candidate.Value, "qualified_item_id")));
            parameters.Add(Parameter("shop_item_id", ReadString(candidate.Value, "item_id")));
            parameters.Add(Parameter("max_unit_price", ReadInt(candidate.Value, "price").ToString()));
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (shopStock.HasValue && shopStock.Value.ValueKind == JsonValueKind.Object)
            {
                parameters.Add(Parameter("expected_shop_id", ReadString(shopStock.Value, "shop_id")));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildRoutePreviewParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("compiler_context.route_graph_source", "locations.route_graph"),
                Parameter("compiler_context.current_map_collision_source", "locations.collision_grid"),
                Parameter("compiler_context.route_executor_enabled", "true")
            };

            var targetLocation = ReadParameter(action, "target_location");
            var targetSummary = ReadRouteMapSummary(snapshot, targetLocation);
            if (targetSummary.HasValue)
            {
                parameters.Add(Parameter("compiler_context.target_location_segment_validation_status", ReadString(targetSummary.Value, "segment_validation_status")));
                parameters.Add(Parameter("compiler_context.target_location_collision_grid_available", ReadBool(targetSummary.Value, "collision_grid_available").ToString().ToLowerInvariant()));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildTraverseConnectorParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters);
            var kind = (ReadParameter(action, "connector_kind") ?? string.Empty).ToLowerInvariant();
            var targetLocation = ReadParameter(action, "expected_target_location") ?? string.Empty;
            var edge = new RouteGraphEdge(
                kind,
                ReadStateFieldString(snapshot, "player", "location_id"),
                targetLocation,
                ReadIntParameter(action, "target_tile_x"),
                ReadIntParameter(action, "target_tile_y"),
                ReadIntParameter(action, "expected_arrival_tile_x"),
                ReadIntParameter(action, "expected_arrival_tile_y"));
            var connector = IsRecoveryConnectorKind(kind)
                ? FindMatchingCurrentRouteConnector(snapshot, edge)
                : null;
            var pathTiles = connector.HasValue
                ? RecoveryConnectorPathTiles(snapshot, edge, connector.Value)
                : null;
            if (pathTiles.HasValue)
            {
                var estimatedTicks = Math.Max(60, (pathTiles.Value + 1) * 60);
                AddParameterIfMissing(parameters, "max_movement_tiles", Math.Max(1, pathTiles.Value + 1).ToString());
                AddParameterIfMissing(parameters, "estimated_ticks", estimatedTicks.ToString());
                AddParameterIfMissing(parameters, "estimated_minutes", Math.Max(1, (estimatedTicks + 59) / 60).ToString());
            }

            AddParameterIfMissing(parameters, "compiler_context.route_connector_source", "locations.route_connectors");
            AddParameterIfMissing(parameters, "compiler_context.current_map_collision_source", "locations.collision_grid");
            AddParameterIfMissing(parameters, "compiler_context.fresh_snapshot_replan_required", "true");
            return parameters.ToArray();
        }

        private static void AddParameterIfMissing(List<SmallModelActionParameter> parameters, string name, string value)
        {
            if (!parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal)))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static SmallModelActionParameter[] BuildMiningReachDepthParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters);
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            var currentDepth = currentMine.HasValue && currentMine.Value.ValueKind == JsonValueKind.Object
                ? ReadInt(currentMine.Value, "mine_level")
                : 0;
            var family = currentMine.HasValue && currentMine.Value.ValueKind == JsonValueKind.Object
                ? ReadString(currentMine.Value, "mine_kind")
                : string.Empty;
            var resources = ReadStateFieldValue(snapshot, "mining", "player_resources");
            var deepestMineLevel = resources.HasValue && resources.Value.ValueKind == JsonValueKind.Object
                ? ReadNullableInt(resources.Value, "deepest_mine_level")
                : null;

            if (currentDepth > 0)
            {
                parameters.Add(Parameter("current_depth", currentDepth.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(family))
            {
                parameters.Add(Parameter("current_mine_kind", family));
            }

            var elevatorStart = MiningReachDepthCandidateBuilder.ElevatorStartFor(currentDepth, ReadIntParameter(action, "target_depth"), family, deepestMineLevel);
            parameters.Add(Parameter("elevator_start_depth", elevatorStart?.ToString() ?? string.Empty));
            var objective = new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.ReachDepth,
                MinimumReserveHealth = ReadIntParameter(action, "minimum_reserve_health") ?? 0,
                MinimumReserveEnergy = ReadIntParameter(action, "minimum_reserve_energy"),
                LatestExitTime = ReadIntParameter(action, "latest_exit_time"),
                TargetDepth = ReadIntParameter(action, "target_depth"),
                ResourcePreservationPolicy =
                    ReadParameter(
                        action,
                        "resource_preservation_policy") ??
                    MiningResourcePreservationPolicies
                        .PreserveStaircases
            };
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, objective);
            parameters.AddRange(MiningFloorStepCompiler.BuildExecutionParameters(floorStep));
            parameters.Add(Parameter("estimate_status", "rolling_horizon_current_floor_step"));
            parameters.Add(Parameter("required_executor_profile", "mining_perfect_executor"));
            parameters.Add(Parameter("runtime_boundary", string.IsNullOrWhiteSpace(MiningFloorStepCompiler.ExecutionOptionId(floorStep)) ? floorStep.Reason : "current_floor_step_executable"));
            parameters.Add(Parameter("compiler_context.transparent_groups", "mining.current_mine,mining.tiles,mining.objects,mining.resource_clumps,mining.monsters,mining.monster_drop_catalogs,mining.floor_objectives,mining.player_resources"));
            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildRecoveryParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters);
            if (Infrastructure.SleepPromptResumeProjection.IsAvailable(
                    snapshot))
            {
                parameters.Add(Parameter(
                    "execution_option_id",
                    "executor.sleep"));
                parameters.Add(Parameter(
                    "sleep_resume_mode",
                    Infrastructure.SleepPromptResumeProjection.ResumeMode));
                parameters.Add(Parameter(
                    "recovery_step_kind",
                    "resume_existing_sleep_prompt"));
                return parameters.ToArray();
            }

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (time < 2200)
            {
                parameters.Add(Parameter("execution_option_id", "executor.wait_ticks"));
                parameters.Add(Parameter("wait_ticks", "30"));
                parameters.Add(Parameter("recovery_step_kind", "refresh_plan_after_stabilization"));
                return parameters.ToArray();
            }

            if (SleepTarget(snapshot) is not null)
            {
                parameters.Add(Parameter("execution_option_id", "executor.sleep"));
                parameters.Add(Parameter("recovery_step_kind", "terminal_sleep"));
                return parameters.ToArray();
            }

            var routePlan = BuildRecoveryRoutePlan(snapshot);
            if (routePlan.Step is null)
            {
                parameters.Add(Parameter("recovery_step_kind", "blocked"));
                parameters.AddRange(routePlan.BlockReasons.Select(reason => Parameter("recovery_block_reason", reason)));
                return parameters.ToArray();
            }

            var step = routePlan.Step;
            parameters.Add(Parameter("execution_option_id", "executor.traverse_connector"));
            parameters.Add(Parameter("recovery_step_kind", "rolling_route_home_connector"));
            parameters.Add(Parameter("target_tile_x", step.Edge.FromX!.Value.ToString()));
            parameters.Add(Parameter("target_tile_y", step.Edge.FromY!.Value.ToString()));
            parameters.Add(Parameter("connector_kind", step.Edge.Kind));
            parameters.Add(Parameter("expected_target_location", step.Edge.TargetLocation));
            parameters.Add(Parameter("max_movement_tiles", Math.Max(1, step.PathTiles + 1).ToString()));
            parameters.Add(Parameter("estimated_ticks", step.EstimatedTicks.ToString()));
            parameters.Add(Parameter("estimated_minutes", Math.Max(1, (step.EstimatedTicks + 59) / 60).ToString()));
            parameters.Add(Parameter("compiler_context.route_graph_source", "locations.route_graph"));
            parameters.Add(Parameter("compiler_context.route_connector_source", "locations.route_connectors"));
            parameters.Add(Parameter("compiler_context.route_gate_source", "locations.route_gate_context"));
            parameters.Add(Parameter("compiler_context.remaining_connector_count", step.RemainingConnectorCount.ToString()));
            if (step.Edge.TargetX.HasValue && step.Edge.TargetY.HasValue)
            {
                parameters.Add(Parameter("expected_arrival_tile_x", step.Edge.TargetX.Value.ToString()));
                parameters.Add(Parameter("expected_arrival_tile_y", step.Edge.TargetY.Value.ToString()));
            }

            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildMiningGoldenScytheParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("target_location_family", "quarry_mine"),
                Parameter("target_mine_level", "77377"),
                Parameter("target_qualified_item_id", "(W)53")
            };
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, MiningGoldenScytheCandidateBuilder.Objective(action.Parameters));
            parameters.AddRange(MiningFloorStepCompiler.BuildExecutionParameters(floorStep));
            parameters.Add(Parameter("estimate_status", "rolling_horizon_current_floor_step"));
            parameters.Add(Parameter("required_executor_profile", "mining_perfect_executor"));
            parameters.Add(Parameter("runtime_boundary", string.IsNullOrWhiteSpace(MiningFloorStepCompiler.ExecutionOptionId(floorStep)) ? floorStep.Reason : "current_floor_step_executable"));
            parameters.Add(Parameter("compiler_context.transparent_groups", "mining.current_mine,mining.tiles,mining.objects,mining.resource_clumps,mining.monsters,mining.floor_objectives,mining.player_resources"));
            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildMiningSkullKeyParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("target_location_family", "ordinary_mines"),
                Parameter("target_depth", "120"),
                Parameter("required_terminal_interaction", "skull_key_reward_chest"),
                Parameter("required_postcondition", "player.has_skull_key=true")
            };
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, MiningSkullKeyCandidateBuilder.Objective(action.Parameters));
            parameters.AddRange(MiningFloorStepCompiler.BuildExecutionParameters(floorStep));
            parameters.Add(Parameter("estimate_status", "rolling_horizon_current_floor_step"));
            parameters.Add(Parameter("required_executor_profile", "mining_perfect_executor"));
            parameters.Add(Parameter("runtime_boundary", string.IsNullOrWhiteSpace(MiningFloorStepCompiler.ExecutionOptionId(floorStep)) ? floorStep.Reason : "current_floor_step_executable"));
            parameters.Add(Parameter("compiler_context.transparent_groups", "player.has_skull_key,mining.current_mine,mining.tiles,mining.objects,mining.resource_clumps,mining.monsters,mining.floor_objectives,mining.player_resources"));
            return parameters.ToArray();
        }

        private static SmallModelActionParameter[] BuildVolcanoReachCalderaParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("target_volcano_level", "9"),
                Parameter("target_location", "Caldera")
            };
            var currentLevel = ReadStateFieldValue(snapshot, "volcano", "current_level");
            if (currentLevel.HasValue && currentLevel.Value.ValueKind == JsonValueKind.Object)
            {
                parameters.Add(Parameter("current_volcano_level", ReadInt(currentLevel.Value, "level").ToString()));
            }

            var floorStep = new VolcanoFloorStepPlanner().Plan(snapshot);
            parameters.AddRange(VolcanoFloorStepCompiler.BuildExecutionParameters(floorStep));
            parameters.Add(Parameter("estimate_status", "rolling_horizon_current_floor_step"));
            parameters.Add(Parameter("required_executor_profile", "volcano_perfect_executor"));
            parameters.Add(Parameter("runtime_boundary", string.IsNullOrWhiteSpace(VolcanoFloorStepCompiler.ExecutionOptionId(floorStep)) ? floorStep.Reason : "current_floor_step_executable"));
            parameters.Add(Parameter("compiler_context.transparent_groups", "volcano.current_level,volcano.tiles,volcano.connectors,volcano.gates,volcano.objects,volcano.monsters,volcano.player_resources"));
            return parameters.ToArray();
        }

        private static JsonElement? ReadRouteMapSummary(SnapshotEnvelope snapshot, string? locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return null;
            }

            var summaries = ReadStateFieldValue(snapshot, "locations", "route_map_summaries");
            if (!summaries.HasValue ||
                summaries.Value.ValueKind != JsonValueKind.Object ||
                !summaries.Value.TryGetProperty("locations", out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var location in locations.EnumerateArray())
            {
                if (location.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(location, "location_id"), locationId, StringComparison.OrdinalIgnoreCase))
                {
                    return location;
                }
            }

            return null;
        }

        private static JsonElement? FirstSafePurchaseCandidate(SnapshotEnvelope snapshot)
        {
            return FindPurchaseCandidate(snapshot, null, null);
        }

        private static JsonElement? FindPurchaseCandidate(SnapshotEnvelope snapshot, string? qualifiedItemId, string? shopItemId)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || ReadBool(entry, "executor_purchase_enabled") != true)
                {
                    continue;
                }

                var qualifiedMatches = string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    string.Equals(ReadString(entry, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase);
                var itemMatches = string.IsNullOrWhiteSpace(shopItemId) ||
                    string.Equals(ReadString(entry, "item_id"), shopItemId, StringComparison.OrdinalIgnoreCase);
                if (qualifiedMatches && itemMatches)
                {
                    return entry;
                }
            }

            return null;
        }

    }
}
