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

namespace StardewAI.Core.Execution
{
    public sealed class ActionQueueCompiler
    {
        private const int DefaultMaxMoveRouteRepairClears = 2;
        private const int HardMaxMoveRouteRepairClears = 4;
        private const int DefaultMoveRouteRepairMinutesPerClear = 2;
        private readonly OptionRegistry.OptionRegistry optionRegistry;
        private readonly Verifier.Verifier verifier;

        public ActionQueueCompiler()
            : this(new OptionRegistry.OptionRegistry(), new Verifier.Verifier())
        {
        }

        public ActionQueueCompiler(OptionRegistry.OptionRegistry optionRegistry, Verifier.Verifier verifier)
        {
            this.optionRegistry = optionRegistry;
            this.verifier = verifier;
        }

        public ActionQueueEnvelope Compile(SmallModelActionEnvelope modelOutput, SnapshotEnvelope snapshot)
        {
            var diagnostics = new List<string>();
            if (modelOutput.SchemaVersion != "small_model_action.v1")
            {
                diagnostics.Add("unsupported_small_model_action_schema:" + modelOutput.SchemaVersion);
            }

            if (!string.Equals(modelOutput.StateHash, snapshot.StateHash, StringComparison.Ordinal))
            {
                diagnostics.Add("state_hash_mismatch");
            }

            if (modelOutput.Actions.Length == 0)
            {
                diagnostics.Add("empty_action_list");
            }

            diagnostics.AddRange(ValidateExecutionTarget(modelOutput.ExecutionMode, modelOutput.Actor));

            var items = modelOutput.Actions
                .Select(action => CompileAction(action, snapshot, modelOutput.ExecutionMode, modelOutput.Actor, diagnostics.Count > 0))
                .ToArray();
            var blocked = diagnostics.Count > 0 || items.Any(item => item.Status == "blocked");

            return new ActionQueueEnvelope
            {
                QueueId = "queue." + Guid.NewGuid().ToString("N"),
                SourceModelOutputId = modelOutput.ModelOutputId,
                SourceModel = modelOutput.SourceModel,
                StateHash = snapshot.StateHash,
                GoalId = modelOutput.GoalId,
                ExecutionMode = modelOutput.ExecutionMode,
                Actor = modelOutput.Actor,
                Status = blocked ? "blocked" : "pending",
                Items = items,
                CompilerDiagnostics = diagnostics.ToArray()
            };
        }

        public ActionQueueEnvelope Compile(SmallModelPlanEnvelope planOutput, SnapshotEnvelope snapshot)
        {
            var actions = new List<SmallModelAction>();
            var activeMenuOpenBeforeStep = ActiveMenuOpen(snapshot);
            var activeMenuTypeBeforeStep = activeMenuOpenBeforeStep ? ActiveMenuType(snapshot) : string.Empty;
            var expandedSteps = ExpandMoveRouteRepairs(planOutput.Steps, snapshot);
            for (var index = 0; index < expandedSteps.Length; index++)
            {
                var step = expandedSteps[index];
                actions.Add(PlanStepToAction(step, index, expandedSteps.Length, activeMenuOpenBeforeStep, activeMenuTypeBeforeStep));
                if (string.Equals(step.Kind, "close_menu", StringComparison.Ordinal))
                {
                    activeMenuOpenBeforeStep = false;
                    activeMenuTypeBeforeStep = string.Empty;
                }
                else if (StepOpensMenu(step))
                {
                    activeMenuOpenBeforeStep = true;
                    activeMenuTypeBeforeStep = InferredOpenedMenuType(step);
                }
            }

            var actionEnvelope = new SmallModelActionEnvelope
            {
                ModelOutputId = string.IsNullOrWhiteSpace(planOutput.PlanId)
                    ? "plan." + Guid.NewGuid().ToString("N")
                    : planOutput.PlanId,
                SourceModel = planOutput.SourceModel,
                StateHash = planOutput.StateHash,
                GoalId = planOutput.GoalId,
                ExecutionMode = planOutput.ExecutionMode,
                Actor = planOutput.Actor,
                Actions = actions.ToArray()
            };

            if (planOutput.SchemaVersion != "small_model_plan.v1")
            {
                actionEnvelope.SchemaVersion = "unsupported_plan_schema:" + planOutput.SchemaVersion;
            }

            var queue = Compile(actionEnvelope, snapshot);
            queue.CandidateAudit = planOutput.CandidateAudit;
            return queue;
        }

        private static SmallModelPlanStep[] ExpandMoveRouteRepairs(SmallModelPlanStep[] steps, SnapshotEnvelope snapshot)
        {
            var expanded = new List<SmallModelPlanStep>();
            foreach (var step in steps)
            {
                if (string.Equals(step.Kind, "move_to_tile", StringComparison.Ordinal))
                {
                    expanded.AddRange(MoveRepairSteps(step, snapshot));
                }

                expanded.Add(step);
            }

            return expanded.ToArray();
        }

        private static SmallModelPlanStep[] MoveRepairSteps(SmallModelPlanStep moveStep, SnapshotEnvelope snapshot)
        {
            if (!moveStep.TargetTileX.HasValue || !moveStep.TargetTileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var repairs = FindMoveRepairObstacles(snapshot, moveStep.TargetTileX.Value, moveStep.TargetTileY.Value, ReadMoveRepairClearLimit(moveStep));
            if (repairs.Length == 0)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var repairMinutes = repairs.Length * DefaultMoveRouteRepairMinutesPerClear;
            var allowedRepairMinutes = ReadMoveRepairMinuteBudget(moveStep);
            if (allowedRepairMinutes.HasValue && repairMinutes > allowedRepairMinutes.Value)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var repairEnergy = repairs.Sum(repair => repair.EnergyCost);
            var availableEnergy = ReadStateFieldDoubleOptional(snapshot, "player", "energy");
            if (availableEnergy.HasValue && repairEnergy > availableEnergy.Value)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            for (var index = 0; index < repairs.Length; index++)
            {
                var repair = repairs[index];
                steps.Add(new SmallModelPlanStep
                {
                    StepId = RepairStepId(moveStep, "move_to_clear_stand", index),
                    Kind = "move_to_tile",
                    TargetLocation = moveStep.TargetLocation,
                    TargetTileX = repair.StandX,
                    TargetTileY = repair.StandY,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "compiler_inserted_move_route_repair=true", "route_repair_index=" + index },
                    ExpectedEffects = new[] { "player.tile=" + repair.StandX + "," + repair.StandY },
                    SafetyConstraints = new[] { "route_repair_stand_tile_reachable_before_clear" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
                steps.Add(new SmallModelPlanStep
                {
                    StepId = RepairStepId(moveStep, "clear_route_obstacle", index),
                    Kind = "clear_obstacle",
                    TargetLocation = moveStep.TargetLocation,
                    TargetTileX = repair.ObstacleX,
                    TargetTileY = repair.ObstacleY,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "compiler_inserted_move_route_repair=true", "route_repair_index=" + index, "target_obstacle_clearable=true", "target_tile_adjacent=true" },
                    ExpectedEffects = new[] { "move_route_repair_for=" + (moveStep.StepId ?? "move_to_tile") + ";current_location.obstacle[" + repair.ObstacleX + "," + repair.ObstacleY + "]=clear" },
                    SafetyConstraints = new[]
                    {
                        "clear_obstacle_from_transparent_current_location_state",
                        "max_route_repair_clears=" + repairs.Length,
                        "route_repair_minutes_budget=" + repairMinutes + "/" + (allowedRepairMinutes.HasValue ? allowedRepairMinutes.Value.ToString() : "unbounded"),
                        "route_repair_energy_budget=" + repairEnergy + "/" + (availableEnergy.HasValue ? availableEnergy.Value.ToString() : "unknown")
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_tool_swings", "8"),
                        Parameter("route_repair_clear_kind", repair.ClearKind),
                        Parameter("route_repair_energy_cost", repair.EnergyCost.ToString())
                    }
                });
            }

            return steps.ToArray();
        }

        private static string RepairStepId(SmallModelPlanStep moveStep, string suffix, int index)
        {
            return (string.IsNullOrWhiteSpace(moveStep.StepId) ? "move_to_tile" : moveStep.StepId) + ".repair." + index + "." + suffix;
        }

        private static int ReadMoveRepairClearLimit(SmallModelPlanStep moveStep)
        {
            var value = moveStep.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, "max_route_repair_clears", StringComparison.OrdinalIgnoreCase))?.Value;
            return int.TryParse(value, out var parsed)
                ? Math.Clamp(parsed, 0, HardMaxMoveRouteRepairClears)
                : DefaultMaxMoveRouteRepairClears;
        }

        private static int? ReadMoveRepairMinuteBudget(SmallModelPlanStep moveStep)
        {
            var explicitValue = moveStep.Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, "max_route_repair_minutes", StringComparison.OrdinalIgnoreCase))?.Value;
            if (int.TryParse(explicitValue, out var explicitParsed))
            {
                return Math.Max(0, explicitParsed);
            }

            return moveStep.EstimatedMinutes.HasValue
                ? Math.Max(0, moveStep.EstimatedMinutes.Value)
                : null;
        }

        private static MoveRepairObstacle[] FindMoveRepairObstacles(SnapshotEnvelope snapshot, int targetX, int targetY, int maxClears)
        {
            if (maxClears <= 0)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var blocked = ReadBlockedCollisionTiles(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTiles(snapshot);
            if (PathExists(startX.Value, startY.Value, targetX, targetY, width, height, blocked, unsupported))
            {
                return Array.Empty<MoveRepairObstacle>();
            }

            var repairs = new List<MoveRepairObstacle>();
            var currentX = startX.Value;
            var currentY = startY.Value;
            var clearableObstacles = ClearableObstacleTiles(snapshot)
                .GroupBy(obstacle => TileKey(obstacle.X, obstacle.Y))
                .Select(group => group.OrderBy(obstacle => obstacle.EnergyCost).First())
                .ToArray();
            while (repairs.Count < maxClears)
            {
                var repair = clearableObstacles
                    .Where(obstacle => blocked.Contains(TileKey(obstacle.X, obstacle.Y)))
                    .Select(obstacle => RepairCandidateForObstacle(currentX, currentY, targetX, targetY, width, height, blocked, unsupported, obstacle))
                    .Where(candidate => candidate is not null)
                    .OrderBy(candidate => Math.Abs(currentX - candidate!.StandX) + Math.Abs(currentY - candidate.StandY))
                    .FirstOrDefault();
                if (repair is null)
                {
                    break;
                }

                repairs.Add(repair);
                blocked.Remove(TileKey(repair.ObstacleX, repair.ObstacleY));
                currentX = repair.StandX;
                currentY = repair.StandY;
                if (PathExists(currentX, currentY, targetX, targetY, width, height, blocked, unsupported))
                {
                    return repairs.ToArray();
                }
            }

            return PathExists(currentX, currentY, targetX, targetY, width, height, blocked, unsupported)
                ? repairs.ToArray()
                : Array.Empty<MoveRepairObstacle>();
        }

        private static MoveRepairObstacle? RepairCandidateForObstacle(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int width,
            int height,
            HashSet<string> blocked,
            HashSet<string> unsupported,
            ClearableObstacle obstacle)
        {
            foreach (var stand in Neighbors(obstacle.X, obstacle.Y)
                .Where(tile => TileInBounds(tile.X, tile.Y, width, height))
                .Where(tile => !blocked.Contains(TileKey(tile.X, tile.Y)))
                .OrderBy(tile => Math.Abs(startX - tile.X) + Math.Abs(startY - tile.Y)))
            {
                if (!PathExists(startX, startY, stand.X, stand.Y, width, height, blocked, unsupported))
                {
                    continue;
                }

                return new MoveRepairObstacle(obstacle.X, obstacle.Y, stand.X, stand.Y, obstacle.ClearKind, obstacle.EnergyCost);
            }

            return null;
        }

        private static IEnumerable<ClearableObstacle> ClearableObstacleTiles(SnapshotEnvelope snapshot)
        {
            var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
            if (objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in objects.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var clearKind = ClearableObjectKind(ReadString(item, "qualified_item_id"), ReadString(item, "name"));
                    if (!string.IsNullOrWhiteSpace(clearKind))
                    {
                        yield return new ClearableObstacle(ReadInt(item, "tile_x"), ReadInt(item, "tile_y"), clearKind, ClearObstacleEnergyCost(clearKind));
                    }
                }
            }

            var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            if (terrainFeatures.HasValue && terrainFeatures.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in terrainFeatures.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var clearKind = ClearableTerrainFeatureKind(ReadString(feature, "type"));
                    if (!string.IsNullOrWhiteSpace(clearKind))
                    {
                        yield return new ClearableObstacle(ReadInt(feature, "tile_x"), ReadInt(feature, "tile_y"), clearKind, ClearObstacleEnergyCost(clearKind));
                    }
                }
            }
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

        private sealed class ClearableObstacle
        {
            public ClearableObstacle(int x, int y, string clearKind, int energyCost)
            {
                X = x;
                Y = y;
                ClearKind = clearKind;
                EnergyCost = energyCost;
            }

            public int X { get; }
            public int Y { get; }
            public string ClearKind { get; }
            public int EnergyCost { get; }
        }

        private sealed class MoveRepairObstacle
        {
            public MoveRepairObstacle(int obstacleX, int obstacleY, int standX, int standY, string clearKind, int energyCost)
            {
                ObstacleX = obstacleX;
                ObstacleY = obstacleY;
                StandX = standX;
                StandY = standY;
                ClearKind = clearKind;
                EnergyCost = energyCost;
            }

            public int ObstacleX { get; }
            public int ObstacleY { get; }
            public int StandX { get; }
            public int StandY { get; }
            public string ClearKind { get; }
            public int EnergyCost { get; }
        }

        private static SmallModelAction PlanStepToAction(
            SmallModelPlanStep step,
            int stepIndex,
            int stepCount,
            bool activeMenuOpenBeforeStep,
            string activeMenuTypeBeforeStep)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("plan_step_kind", step.Kind),
                Parameter("target_location", step.TargetLocation),
                Parameter("compiler_context.plan_step_index", stepIndex.ToString()),
                Parameter("compiler_context.plan_step_count", stepCount.ToString()),
                Parameter("compiler_context.is_terminal_step", (stepIndex == stepCount - 1).ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_open_before_step", activeMenuOpenBeforeStep.ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_type_before_step", activeMenuTypeBeforeStep)
            };

            if (step.TargetTileX.HasValue)
            {
                parameters.Add(Parameter("target_tile_x", step.TargetTileX.Value.ToString()));
            }
            if (step.TargetTileY.HasValue)
            {
                parameters.Add(Parameter("target_tile_y", step.TargetTileY.Value.ToString()));
            }
            if (step.Direction.HasValue)
            {
                parameters.Add(Parameter("direction", step.Direction.Value.ToString()));
            }
            if (step.WaitTicks.HasValue)
            {
                parameters.Add(Parameter("wait_ticks", step.WaitTicks.Value.ToString()));
            }
            if (step.EstimatedMinutes.HasValue)
            {
                parameters.Add(Parameter("estimated_minutes", step.EstimatedMinutes.Value.ToString()));
            }
            parameters.AddRange(step.Preconditions.Select(value => Parameter("precondition", value)));
            parameters.AddRange(step.ExpectedEffects.Select(value => Parameter("expected_effect", value)));
            parameters.AddRange(step.SafetyConstraints.Select(value => Parameter("safety_constraint", value)));
            parameters.AddRange(step.FailurePolicy.Select(value => Parameter("failure_policy", value)));
            parameters.AddRange(step.Parameters);

            return new SmallModelAction
            {
                ActionId = string.IsNullOrWhiteSpace(step.StepId) ? "plan_step." + Guid.NewGuid().ToString("N") : step.StepId,
                OptionId = PlanStepOptionId(step.Kind),
                Rationale = "compiled from small_model_plan step",
                Parameters = parameters.ToArray()
            };
        }

        private static string PlanStepOptionId(string kind)
        {
            return kind switch
            {
                "move_to_tile" => "executor.move_to_tile",
                "traverse_connector" => "executor.traverse_connector",
                "face_direction" => "executor.face_direction",
                "interact" => "executor.interact",
                "sleep" => "executor.sleep",
                "wait_ticks" => "executor.wait_ticks",
                "close_menu" => "executor.close_menu",
                "buy_shop_item" => "executor.buy_shop_item",
                "choose_dialogue_response" => "executor.choose_dialogue_response",
                "maintain_crops" => "farm.maintain_crops",
                "clear_obstacle" => "executor.clear_obstacle",
                "till_soil" => "executor.till_soil",
                "plant_seed" => "executor.plant_seed",
                "harvest_crop" => "executor.harvest_crop",
                "harvest_giant_crop" => "executor.harvest_giant_crop",
                "pickup_debris" => "executor.pickup_debris",
                "collect_machine_output" => "executor.collect_machine_output",
                "load_machine_input" => "executor.load_machine_input",
                "catch_fish" => "executor.catch_fish",
                "social_interact" => "executor.social_interact",
                "ship_inventory_item_to_bin" => "executor.ship_inventory_item_to_bin",
                _ => "unknown.plan_step"
            };
        }

        private static bool StepOpensMenu(SmallModelPlanStep step)
        {
            return step.ExpectedEffects.Any(effect =>
                effect.Contains("menus.active_menu.is_open=true", StringComparison.OrdinalIgnoreCase) ||
                effect.Contains("menus.sleep_prompt_context.prompt_open=true", StringComparison.OrdinalIgnoreCase) ||
                effect.Contains("menu_open", StringComparison.OrdinalIgnoreCase));
        }

        private static string InferredOpenedMenuType(SmallModelPlanStep step)
        {
            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("DialogueBox", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("dialogue", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Blacksmith", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Carpenter", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Marnie", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_AdventureGuild", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_adventureGuild", StringComparison.OrdinalIgnoreCase)))
            {
                return "DialogueBox";
            }

            if (step.ExpectedEffects.Any(effect =>
                    effect.Contains("ShopMenu", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_OpenShop", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_Buy", StringComparison.OrdinalIgnoreCase) ||
                    effect.Contains("interact_map_action_JojaShop", StringComparison.OrdinalIgnoreCase)))
            {
                return "ShopMenu";
            }

            return string.Empty;
        }

        private static string[] ValidateExecutionTarget(string executionMode, ActionActorRef actor)
        {
            var errors = new List<string>();
            if (!string.Equals(executionMode, "training_singleplayer", StringComparison.Ordinal) &&
                !string.Equals(executionMode, "coop_companion", StringComparison.Ordinal))
            {
                errors.Add("unsupported_execution_mode:" + executionMode);
            }

            if (string.IsNullOrWhiteSpace(actor.ActorId))
            {
                errors.Add("actor_id_required");
            }

            if (string.Equals(actor.ActorType, "human_player", StringComparison.Ordinal))
            {
                errors.Add("actor_type_human_player_forbidden");
            }

            if (string.Equals(actor.ControlSurface, "keyboard_mouse", StringComparison.Ordinal))
            {
                errors.Add("control_surface_keyboard_mouse_forbidden");
            }

            if (string.Equals(executionMode, "training_singleplayer", StringComparison.Ordinal))
            {
                if (!string.Equals(actor.ActorType, "training_farmer", StringComparison.Ordinal))
                {
                    errors.Add("training_singleplayer_requires_training_farmer");
                }

                if (!string.Equals(actor.ControlSurface, "training_sandbox", StringComparison.Ordinal))
                {
                    errors.Add("training_singleplayer_requires_training_sandbox");
                }
            }

            if (string.Equals(executionMode, "coop_companion", StringComparison.Ordinal))
            {
                if (!string.Equals(actor.ActorType, "ai_companion", StringComparison.Ordinal))
                {
                    errors.Add("coop_companion_requires_ai_companion");
                }

                if (!string.Equals(actor.ControlSurface, "companion_actor", StringComparison.Ordinal))
                {
                    errors.Add("coop_companion_requires_companion_actor");
                }
            }

            return errors.ToArray();
        }

        private ActionQueueItem CompileAction(SmallModelAction action, SnapshotEnvelope snapshot, string executionMode, ActionActorRef actor, bool globallyBlocked)
        {
            var blocking = new List<string>();
            SafetyResult safety;
            string[] requiredFactors;
            OptionSpec? option = null;
            try
            {
                option = optionRegistry.GetRequired(action.OptionId);
                safety = verifier.Verify(snapshot, option);
                requiredFactors = option.RequiredStateFactors;
                blocking.AddRange(safety.BlockingReasons);
            }
            catch (KeyNotFoundException)
            {
                safety = new SafetyResult
                {
                    Feasibility = "unknown",
                    MissingStateFactors = Array.Empty<string>(),
                    PreconditionResults = Array.Empty<PreconditionResult>(),
                    BlockingReasons = new[] { "unknown_option_id" }
                };
                requiredFactors = Array.Empty<string>();
                blocking.Add("unknown_option_id");
            }

            if (globallyBlocked)
            {
                blocking.Add("queue_global_compiler_block");
            }

            var (strategyBlocking, validatedDirection) = ValidateStrategyPlan(action, option, snapshot, executionMode);
            blocking.AddRange(strategyBlocking);

            blocking.AddRange(ValidateSocialPlan(action, snapshot));
            blocking.AddRange(ValidateSocialInteractPlan(action, snapshot));
            blocking.AddRange(ValidateRecoveryPlan(action, snapshot));
            blocking.AddRange(ValidateRouteActionBranches(action, snapshot));
            blocking.AddRange(ValidateRoutePathPreview(action, snapshot));
            blocking.AddRange(ValidateRouteGraphPreview(action, snapshot));
            blocking.AddRange(ValidateMovementPlan(action));
            blocking.AddRange(ValidateClearObstaclePlan(action));
            blocking.AddRange(ValidateTillSoilPlan(action, snapshot));
            blocking.AddRange(ValidatePlantSeedPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestCropPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestGiantCropPlan(action, snapshot));
            blocking.AddRange(ValidatePickupDebrisPlan(action, snapshot));
            blocking.AddRange(ValidateCollectMachineOutputPlan(action, snapshot));
            blocking.AddRange(ValidateLoadMachineInputPlan(action, snapshot));
            blocking.AddRange(ValidateConnectorPlan(action));
            blocking.AddRange(ValidateFaceDirectionPlan(action));
            blocking.AddRange(ValidateInteractPlan(action, snapshot));
            blocking.AddRange(ValidateSleepPlan(action, snapshot));
            blocking.AddRange(ValidateWaitTicksPlan(action));
            blocking.AddRange(ValidateCatchFishPlan(action, snapshot));
            blocking.AddRange(ValidateMiningReachDepthPlan(action, snapshot));
            blocking.AddRange(ValidateMiningGoldenScythePlan(action, snapshot));
            blocking.AddRange(ValidateVolcanoReachCalderaPlan(action, snapshot));
            blocking.AddRange(ValidateCoolVolcanoLavaPlan(action, snapshot));
            blocking.AddRange(ValidateSelectSafeItemSlotPlan(action, snapshot));
            blocking.AddRange(ValidateCloseMenuPlan(action, snapshot));
            blocking.AddRange(ValidateBuyShopItemPlan(action, snapshot));
            blocking.AddRange(ValidateChooseDialogueResponsePlan(action, snapshot));
            blocking.AddRange(ValidateQuestAdvancePlan(action, snapshot));
            blocking.AddRange(ValidateActiveMenuBracket(action, snapshot, option));

            var status = blocking.Count == 0 && safety.Feasibility == "feasible"
                ? "pending"
                : "blocked";

            var strategyPlan = status == "pending" && validatedDirection is not null
                ? CompileStrategyPlan(validatedDirection)
                : Array.Empty<StrategyPlanStep>();

            return new ActionQueueItem
            {
                QueueItemId = "queue_item." + Guid.NewGuid().ToString("N"),
                SourceActionId = action.ActionId,
                OptionId = action.OptionId,
                Status = status,
                BehaviorCategory = option?.BehaviorCategory ?? OptionBehaviorCategories.Unknown,
                CompilerResponsibility = option?.CompilerResponsibility ?? CompilerResponsibilities.Unknown,
                TrainingRole = option?.TrainingRole ?? TrainingRoles.Unknown,
                RequiredStateFactors = requiredFactors,
                MissingStateFactors = safety.MissingStateFactors,
                PreconditionResults = safety.PreconditionResults
                    .Select(result => new ActionQueuePrecondition
                    {
                        StateFactor = result.StateFactor,
                        Status = result.Status,
                        Message = result.Message
                    })
                    .ToArray(),
                BlockingReasons = blocking.Distinct(StringComparer.Ordinal).ToArray(),
                NormalizedCommand = new NormalizedCommand
                {
                    CommandType = option?.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion
                        ? "compiled_action_steps"
                        : IsStrategyPlanOption(option, action)
                            ? "strategy_plan"
                        : "option_request",
                    OptionId = action.OptionId,
                    BehaviorCategory = option?.BehaviorCategory ?? OptionBehaviorCategories.Unknown,
                    CompilerResponsibility = option?.CompilerResponsibility ?? CompilerResponsibilities.Unknown,
                    TrainingRole = option?.TrainingRole ?? TrainingRoles.Unknown,
                    StateHash = snapshot.StateHash,
                    ExecutionMode = executionMode,
                    Actor = actor,
                    Parameters = BuildNormalizedParameters(action, snapshot),
                    Steps = CompileSteps(action, snapshot, option),
                    StrategyPlan = strategyPlan,
                    SocialPlan = CompileSocialPlan(action, snapshot),
                    QuestPlan = CompileQuestPlan(action, snapshot)
                }
            };
        }

        private static SmallModelActionParameter[] BuildNormalizedParameters(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId == "exploration.visit_location")
            {
                return BuildRoutePreviewParameters(action, snapshot);
            }

            if (action.OptionId == "executor.select_safe_item_slot")
            {
                return BuildSelectSafeItemSlotParameters(action, snapshot);
            }

            if (action.OptionId == "executor.close_menu")
            {
                return BuildCloseMenuParameters(action, snapshot);
            }

            if (action.OptionId == "mining.reach_depth")
            {
                return BuildMiningReachDepthParameters(action, snapshot);
            }

            if (action.OptionId == "mining.acquire_golden_scythe")
            {
                return BuildMiningGoldenScytheParameters(action, snapshot);
            }

            if (action.OptionId == "volcano.reach_caldera")
            {
                return BuildVolcanoReachCalderaParameters(action, snapshot);
            }

            if (action.OptionId == "executor.buy_shop_item")
            {
                return BuildBuyShopItemParameters(action, snapshot);
            }

            if (action.OptionId == "social.talk_npc" || action.OptionId == "social.gift_npc")
            {
                return BuildSocialParameters(action, snapshot);
            }

            if (action.OptionId != "farm.maintain_crops")
            {
                return action.Parameters;
            }

            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("compiler_context.season", ReadStateFieldString(snapshot, "time", "season")),
                Parameter("compiler_context.weather", ReadStateFieldString(snapshot, "time", "weather")),
                Parameter("compiler_context.crop_count", CountCrops(snapshot).ToString()),
                Parameter("compiler_context.watering_candidate_count", CountWateringCandidates(snapshot).ToString()),
                Parameter("hard_rule.crop_watering_source", "HoeDirt.needsWatering")
            };

            return parameters.ToArray();
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
            var parameters = new List<SmallModelActionParameter>(action.Parameters)
            {
                Parameter("compiler_context.active_menu_open", ActiveMenuOpen(snapshot).ToString().ToLowerInvariant()),
                Parameter("compiler_context.active_menu_type", ActiveMenuType(snapshot)),
                Parameter("compiler_context.close_menu_executor", "Game1.exitActiveMenu")
            };

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
                Parameter("compiler_context.route_executor_enabled", "false")
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
                TargetDepth = ReadIntParameter(action, "target_depth")
            };
            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, objective);
            parameters.AddRange(MiningFloorStepCompiler.BuildExecutionParameters(floorStep));
            parameters.Add(Parameter("estimate_status", "rolling_horizon_current_floor_step"));
            parameters.Add(Parameter("required_executor_profile", "mining_perfect_executor"));
            parameters.Add(Parameter("runtime_boundary", string.IsNullOrWhiteSpace(MiningFloorStepCompiler.ExecutionOptionId(floorStep)) ? floorStep.Reason : "current_floor_step_executable"));
            parameters.Add(Parameter("compiler_context.transparent_groups", "mining.current_mine,mining.tiles,mining.objects,mining.monsters,mining.monster_drop_catalogs,mining.floor_objectives,mining.player_resources"));
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
            parameters.Add(Parameter("compiler_context.transparent_groups", "mining.current_mine,mining.tiles,mining.objects,mining.monsters,mining.floor_objectives,mining.player_resources"));
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

        private static string[] ValidateFaceDirectionPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.face_direction")
            {
                return Array.Empty<string>();
            }

            var direction = ReadIntParameter(action, "direction");
            return direction is >= 0 and <= 3 ? Array.Empty<string>() : new[] { "direction_0_3_required" };
        }

        private static string[] ValidateRecoveryPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "recovery.stabilize_day")
            {
                return Array.Empty<string>();
            }

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (time < 2200)
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("sleep_prompt_menu_must_be_clear");
            }

            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            if (prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open"))
            {
                reasons.Add("sleep_confirm_executor_requires_compiler_terminal_macro");
            }

            if (SleepTarget(snapshot) is null)
            {
                reasons.Add("sleep_target_unavailable");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateWaitTicksPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.wait_ticks")
            {
                return Array.Empty<string>();
            }

            var waitTicks = ReadIntParameter(action, "wait_ticks");
            return waitTicks is >= 1 and <= 600 ? Array.Empty<string>() : new[] { "wait_ticks_1_600_required" };
        }

        private static string[] ValidateSelectSafeItemSlotPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.select_safe_item_slot")
            {
                return Array.Empty<string>();
            }

            var safeSlot = ReadIntParameter(action, "safe_slot_index") ?? SafeSlotIndex(snapshot);
            if (!safeSlot.HasValue)
            {
                return new[] { "safe_item_slot_unavailable" };
            }

            return safeSlot.Value is >= 0 and <= 11
                ? Array.Empty<string>()
                : new[] { "safe_slot_index_0_11_required" };
        }

        private static string[] ValidateCloseMenuPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.close_menu")
            {
                return Array.Empty<string>();
            }

            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            if (prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open"))
            {
                return new[] { "close_menu_sleep_prompt_unsupported" };
            }

            if (!ActiveMenuOpen(snapshot))
            {
                return Array.Empty<string>();
            }

            var type = ActiveMenuType(snapshot);
            if (string.IsNullOrWhiteSpace(type))
            {
                return new[] { "close_menu_type_unknown" };
            }

            if (IsSafeCloseMenuType(type))
            {
                return Array.Empty<string>();
            }

            if (type == "DialogueBox")
            {
                return SafeOrdinaryDialogueBlockReasons(snapshot);
            }

            return new[] { "close_menu_type_not_whitelisted" };
        }

        private static string[] SafeOrdinaryDialogueBlockReasons(SnapshotEnvelope snapshot)
        {
            var reasons = new List<string>();
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue || activeMenu.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { "dialogue_close_transparent_active_menu_fields_missing" };
            }

            var lastQuestionKey = ReadString(activeMenu.Value, "last_question_key");
            if (!string.IsNullOrWhiteSpace(lastQuestionKey))
            {
                reasons.Add("dialogue_close_last_question_key_present:" + lastQuestionKey);
            }

            if (ReadBool(activeMenu.Value, "is_sleep_prompt"))
            {
                reasons.Add("dialogue_close_is_sleep_prompt");
            }

            var eventUp = ReadNullableBool(activeMenu.Value, "event_up");
            if (eventUp is null)
            {
                reasons.Add("dialogue_close_event_up_field_missing_or_ambiguous");
            }
            else if (eventUp.Value)
            {
                reasons.Add("dialogue_close_event_up_true");
            }

            var isQuestion = ReadNullableBool(activeMenu.Value, "dialogue_is_question");
            if (isQuestion is null)
            {
                reasons.Add("dialogue_close_is_question_field_missing_or_ambiguous");
            }
            else if (isQuestion.Value)
            {
                reasons.Add("dialogue_close_is_question_true");
            }

            var responseCount = ReadNullableInt(activeMenu.Value, "dialogue_response_count");
            if (responseCount is null)
            {
                reasons.Add("dialogue_close_response_count_field_missing_or_ambiguous");
            }
            else if (responseCount.Value > 0)
            {
                reasons.Add("dialogue_close_responses_present:" + responseCount.Value);
            }

            var transitioning = ReadNullableBool(activeMenu.Value, "dialogue_transitioning");
            if (transitioning is null)
            {
                reasons.Add("dialogue_close_transitioning_field_missing_or_ambiguous");
            }

            var characterPresent = ReadNullableBool(activeMenu.Value, "dialogue_character_present");
            if (characterPresent is null)
            {
                reasons.Add("dialogue_close_character_present_field_missing_or_ambiguous");
            }
            else if (!characterPresent.Value)
            {
                reasons.Add("dialogue_close_character_present_false");
            }

            var speakerName = ReadString(activeMenu.Value, "dialogue_speaker_name");
            var speakerNamePresent = activeMenu.Value.TryGetProperty("dialogue_speaker_name", out _);
            if (!speakerNamePresent)
            {
                reasons.Add("dialogue_close_speaker_name_field_missing");
            }
            else if (string.IsNullOrWhiteSpace(speakerName))
            {
                reasons.Add("dialogue_close_speaker_name_empty");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool? ReadNullableBool(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return null;
        }

        private static string[] ValidateBuyShopItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.buy_shop_item")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (!ActionSeesShopMenuOpen(action, snapshot))
            {
                reasons.Add("shop_menu_not_open");
            }

            var quantity = ReadIntParameter(action, "quantity") ?? 1;
            if (quantity != 1)
            {
                reasons.Add("quantity_one_required_for_safe_purchase_slice");
            }

            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue || shopStock.Value.ValueKind != JsonValueKind.Object)
            {
                if (HasRuntimeShopStockRecheckParameters(action))
                {
                    return reasons.Distinct(StringComparer.Ordinal).ToArray();
                }

                reasons.Add("menus_shop_stock_unavailable");
                return reasons.ToArray();
            }

            var candidate = FindPurchaseCandidate(snapshot, ReadParameter(action, "qualified_item_id"), ReadParameter(action, "shop_item_id"))
                ?? FirstSafePurchaseCandidate(snapshot);
            if (candidate is null)
            {
                reasons.Add("no_safe_executor_purchase_candidate");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            if (ReadBool(candidate.Value, "executor_purchase_enabled") != true)
            {
                reasons.Add("purchase_candidate_not_executor_enabled");
            }

            var maxUnitPrice = ReadIntParameter(action, "max_unit_price");
            var price = ReadInt(candidate.Value, "price");
            if (maxUnitPrice.HasValue && price > maxUnitPrice.Value)
            {
                reasons.Add("purchase_price_exceeds_request_limit");
            }

            var expectedShopId = ReadParameter(action, "expected_shop_id");
            var actualShopId = ReadString(shopStock.Value, "shop_id");
            if (!string.IsNullOrWhiteSpace(expectedShopId) &&
                !string.Equals(expectedShopId, actualShopId, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("shop_menu_id_mismatch");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateChooseDialogueResponsePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.choose_dialogue_response")
            {
                return Array.Empty<string>();
            }

            if (!ActionSeesDialogueBoxOpen(action, snapshot))
            {
                return new[] { "dialogue_box_not_open" };
            }

            var expectedDialogueKey = ReadParameter(action, "expected_dialogue_key");
            var responseKey = ReadParameter(action, "dialogue_response_key");
            var expectedShopId = ReadParameter(action, "expected_shop_id");
            if (!DialogueResponseOpensExpectedShop(expectedDialogueKey, responseKey, expectedShopId))
            {
                return new[] { "dialogue_response_not_whitelisted" };
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateQuestAdvancePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "quest.advance")
            {
                return Array.Empty<string>();
            }

            var candidateId = ReadParameter(action, "candidate_id");
            var questId = ReadParameter(action, "quest_id");
            var questKey = ReadParameter(action, "quest_key");
            if (string.IsNullOrWhiteSpace(candidateId) && string.IsNullOrWhiteSpace(questId) && string.IsNullOrWhiteSpace(questKey))
            {
                return new[] { "quest_identity_not_specified" };
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateCatchFishPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.catch_fish")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var locationId = ReadParameter(action, "location_id") ?? ReadParameter(action, "target_location") ?? string.Empty;
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var bobberX = ReadIntParameter(action, "bobber_tile_x");
            var bobberY = ReadIntParameter(action, "bobber_tile_y");
            var rodSlot = ReadIntParameter(action, "rod_slot_index");
            var ruleKey = ReadParameter(action, "rule_key") ?? string.Empty;
            var expectedQualifiedItemId = ReadParameter(action, "expected_qualified_item_id") ?? string.Empty;
            var outcomeDistributionJson = ReadParameter(action, "outcome_distribution_json") ?? string.Empty;
            var distributionComplete = bool.TryParse(ReadParameter(action, "outcome_distribution_complete"), out var parsedDistributionComplete) && parsedDistributionComplete;
            var castDirection = ReadIntParameter(action, "cast_direction");
            var targetCastingPower = ReadDoubleParameter(action, "target_casting_power");
            var maxCastRequested = bool.TryParse(ReadParameter(action, "max_cast_requested"), out var parsedMaxCastRequested) && parsedMaxCastRequested;

            if (string.IsNullOrWhiteSpace(locationId)) reasons.Add("fishing_location_id_required");
            if (!standX.HasValue || !standY.HasValue) reasons.Add("fishing_stand_tile_required");
            if (!bobberX.HasValue || !bobberY.HasValue) reasons.Add("fishing_bobber_tile_required");
            if (!rodSlot.HasValue) reasons.Add("fishing_rod_slot_required");
            if (string.IsNullOrWhiteSpace(ruleKey)) reasons.Add("fishing_rule_key_required");
            if (!ruleKey.StartsWith("distribution:", StringComparison.Ordinal)) reasons.Add("fishing_distribution_key_required");
            if (!distributionComplete) reasons.Add("fishing_outcome_distribution_incomplete");
            if (string.IsNullOrWhiteSpace(outcomeDistributionJson)) reasons.Add("fishing_outcome_distribution_required");
            if (!string.IsNullOrWhiteSpace(expectedQualifiedItemId)) reasons.Add("fishing_expected_item_must_be_unconstrained");
            if (reasons.Count > 0)
            {
                return reasons.ToArray();
            }

            if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            {
                reasons.Add("fishing_location_mismatch");
            }
            if ((ReadStateFieldDoubleOptional(snapshot, "player", "energy") ?? 0) <= 1)
            {
                reasons.Add("fishing_energy_too_low");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("active_menu_blocks_fishing");
            }

            var activeCast = ReadStateFieldValue(snapshot, "fishing", "active_cast_state");
            if (activeCast.HasValue && ReadBool(activeCast.Value, "in_use"))
            {
                reasons.Add("fishing_rod_already_in_use");
            }

            var dx = bobberX!.Value - standX!.Value;
            var dy = bobberY!.Value - standY!.Value;
            if (dx != 0 && dy != 0 || dx == 0 && dy == 0)
            {
                reasons.Add("fishing_cast_not_cardinal");
            }
            else
            {
                var direction = dx > 0 ? 1 : dx < 0 ? 3 : dy > 0 ? 2 : 0;
                var distance = Math.Abs(dx) + Math.Abs(dy);
                var fishingLevel = ReadFishingLevel(snapshot);
                var addedDistance = fishingLevel >= 15 ? 4 : fishingLevel >= 8 ? 3 : fishingLevel >= 4 ? 2 : fishingLevel >= 1 ? 1 : 0;
                var maxDistance = direction is 1 or 3 ? addedDistance + 4 : addedDistance + 3;
                var computedPower = Math.Clamp(distance / (double)maxDistance, 0d, 1d);
                if (distance < 2 || distance > maxDistance)
                {
                    reasons.Add("fishing_cast_out_of_range");
                }
                if (castDirection.HasValue && castDirection.Value != direction)
                {
                    reasons.Add("fishing_cast_direction_mismatch");
                }
                if (targetCastingPower.HasValue && Math.Abs(targetCastingPower.Value - computedPower) > 0.001d)
                {
                    reasons.Add("fishing_target_casting_power_mismatch");
                }
                if (maxCastRequested != (distance == maxDistance))
                {
                    reasons.Add("fishing_max_cast_requested_mismatch");
                }
            }

            if (FishingCollisionGridBlocks(snapshot, standX.Value, standY.Value))
            {
                reasons.Add("fishing_stand_tile_collision_blocked");
            }

            var rods = ReadStateFieldValue(snapshot, "fishing", "rod_inventory");
            var rodExists = rods.HasValue && rods.Value.ValueKind == JsonValueKind.Array && rods.Value.EnumerateArray().Any(rod =>
                ReadInt(rod, "slot_index") == rodSlot!.Value &&
                !string.IsNullOrWhiteSpace(ReadString(rod, "qualified_item_id")));
            if (!rodExists)
            {
                reasons.Add("fishing_rod_slot_not_found");
            }

            var fishableTiles = ReadStateFieldValue(snapshot, "fishing", "fishable_tiles");
            var bobberTileIndex = -1;
            var bobberWaterDepth = 0;
            if (fishableTiles.HasValue && fishableTiles.Value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var tile in fishableTiles.Value.EnumerateArray())
                {
                    if (ReadInt(tile, "tile_x") == bobberX.Value && ReadInt(tile, "tile_y") == bobberY.Value)
                    {
                        bobberTileIndex = index;
                        bobberWaterDepth = ReadInt(tile, "water_depth");
                        break;
                    }
                    index++;
                }
            }
            if (bobberTileIndex < 0)
            {
                reasons.Add("fishing_bobber_tile_not_fishable");
            }

            var transparentContextAllows = distributionComplete && !string.IsNullOrWhiteSpace(outcomeDistributionJson) &&
                FishingDistributionContextAllows(
                    snapshot,
                    locationId,
                    rodSlot.GetValueOrDefault(),
                    standX.GetValueOrDefault(),
                    standY.GetValueOrDefault(),
                    bobberX.GetValueOrDefault(),
                    bobberY.GetValueOrDefault(),
                    ruleKey,
                    outcomeDistributionJson);
            if (!transparentContextAllows)
            {
                reasons.Add("fishing_rule_context_no_longer_allows_candidate");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateMiningReachDepthPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "mining.reach_depth")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var targetDepth = ReadIntParameter(action, "target_depth");
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            var currentDepth = currentMine.HasValue ? ReadInt(currentMine.Value, "mine_level") : 0;
            var currentFamily = currentMine.HasValue ? ReadString(currentMine.Value, "mine_kind") : string.Empty;
            reasons.AddRange(MiningReachDepthCandidateBuilder.ValidateTarget(currentDepth, currentFamily, targetDepth, ReadParameter(action, "target_location_family")));

            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.ReachDepth,
                MinimumReserveHealth = ReadIntParameter(action, "minimum_reserve_health") ?? 0,
                MinimumReserveEnergy = ReadIntParameter(action, "minimum_reserve_energy"),
                LatestExitTime = ReadIntParameter(action, "latest_exit_time"),
                TargetDepth = targetDepth
            });
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add(floorStep.StepKind == MiningFloorStepKinds.DescendLadder
                    ? "mining_descend_ladder_executor_not_implemented"
                    : "mining_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateMiningGoldenScythePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "mining.acquire_golden_scythe")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (currentMine.HasValue)
            {
                reasons.AddRange(MiningGoldenScytheCandidateBuilder.ValidateCurrentMine(currentMine.Value));
            }

            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, MiningGoldenScytheCandidateBuilder.Objective(action.Parameters));
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add("golden_scythe_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateVolcanoReachCalderaPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "volcano.reach_caldera")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(VolcanoReachCalderaCandidateBuilder.MissingVolcanoGroups(snapshot));
            var floorStep = new VolcanoFloorStepPlanner().Plan(snapshot);
            var executionOptionId = VolcanoFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add("volcano_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateCoolVolcanoLavaPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.cool_volcano_lava")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var wateringCanSlot = ReadIntParameter(action, "watering_can_slot_index");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("volcano_cooling_target_tile_required");
            }
            if (!standX.HasValue || !standY.HasValue)
            {
                reasons.Add("volcano_cooling_stand_tile_required");
            }
            else if (targetX.HasValue && targetY.HasValue &&
                Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            {
                reasons.Add("volcano_cooling_stand_tile_not_adjacent");
            }
            if (!wateringCanSlot.HasValue)
            {
                reasons.Add("volcano_cooling_watering_can_slot_required");
            }

            var currentLevel = ReadStateFieldValue(snapshot, "volcano", "current_level");
            if (!currentLevel.HasValue || currentLevel.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("volcano_current_level_unavailable");
            }
            else if (ReadInt(currentLevel.Value, "level") == 5 || !ReadBool(currentLevel.Value, "lava_cooling_enabled"))
            {
                reasons.Add("volcano_level_five_cooling_disabled");
            }

            var tiles = ReadStateFieldValue(snapshot, "volcano", "tiles");
            if (targetX.HasValue && targetY.HasValue &&
                (!tiles.HasValue || tiles.Value.ValueKind != JsonValueKind.Object ||
                 !tiles.Value.TryGetProperty("coolable_uncooled_tiles", out var coolable) ||
                 coolable.ValueKind != JsonValueKind.Array ||
                 !coolable.EnumerateArray().Any(tile =>
                     ReadInt(tile, "tile_x") == targetX.Value && ReadInt(tile, "tile_y") == targetY.Value)))
            {
                reasons.Add("volcano_cooling_target_not_currently_coolable");
            }

            var resources = ReadStateFieldValue(snapshot, "volcano", "player_resources");
            if (wateringCanSlot.HasValue &&
                (!resources.HasValue || resources.Value.ValueKind != JsonValueKind.Object ||
                 !resources.Value.TryGetProperty("watering_can_slots", out var wateringCans) ||
                 wateringCans.ValueKind != JsonValueKind.Array ||
                 !wateringCans.EnumerateArray().Any(slot =>
                     ReadInt(slot, "slot_index") == wateringCanSlot.Value && ReadBool(slot, "can_cool_lava_now"))))
            {
                reasons.Add("volcano_cooling_watering_can_unavailable_or_empty");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool FishingDistributionContextAllows(
            SnapshotEnvelope snapshot,
            string locationId,
            int rodSlot,
            int standX,
            int standY,
            int bobberX,
            int bobberY,
            string distributionKey,
            string outcomeDistributionJson)
        {
            return FishingEventCandidateBuilder.Build(snapshot).Any(candidate =>
                candidate.Available &&
                candidate.Kind == "catch_fish" &&
                string.Equals(candidate.LocationId, locationId, StringComparison.Ordinal) &&
                candidate.SlotIndex == rodSlot &&
                candidate.TileX == standX &&
                candidate.TileY == standY &&
                FishingCandidateParameterInt(candidate, "bobber_tile_x") == bobberX &&
                FishingCandidateParameterInt(candidate, "bobber_tile_y") == bobberY &&
                string.Equals(FishingCandidateParameter(candidate, "rule_key"), distributionKey, StringComparison.Ordinal) &&
                string.Equals(FishingCandidateParameter(candidate, "outcome_distribution_json"), outcomeDistributionJson, StringComparison.Ordinal));
        }

        private static string FishingCandidateParameter(EventCandidate candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;
        }

        private static int? FishingCandidateParameterInt(EventCandidate candidate, string name)
        {
            return int.TryParse(FishingCandidateParameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static bool FishingRuleContextAllows(
            SnapshotEnvelope snapshot,
            int rodSlot,
            string ruleKey,
            string expectedQualifiedItemId,
            int bobberTileIndex,
            int standX,
            int standY)
        {
            if (bobberTileIndex < 0 || !FishingBaseCatchPathAllows(snapshot, rodSlot, bobberTileIndex))
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("spawn_rules", out var spawnRules) ||
                    spawnRules.ValueKind != JsonValueKind.Object ||
                    !ReadBool(spawnRules, "item_query_resolution_complete") ||
                    !spawnRules.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var rule in rules.EnumerateArray())
                {
                    if (!string.Equals(ReadString(rule, "rule_key"), ruleKey, StringComparison.Ordinal) ||
                        !ReadBool(rule, "condition_met") ||
                        FishingRuleHasFixedBlock(rule) ||
                        !FishingPlayerRectangleAllows(rule, standX, standY) ||
                        !JsonIntArrayContains(rule, "eligible_fishable_tile_indices", bobberTileIndex) ||
                        !rule.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    return outputs.EnumerateArray().Any(output =>
                        ReadBool(output, "resolution_complete") &&
                        ReadBool(output, "output_eligible_before_random_rolls") &&
                        (string.IsNullOrWhiteSpace(expectedQualifiedItemId) ||
                         string.Equals(ReadString(output, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal)));
                }
            }
            return false;
        }

        private static bool FishingSpecialContextAllows(
            SnapshotEnvelope snapshot,
            int rodSlot,
            string specialSource,
            string expectedQualifiedItemId,
            int bobberTileIndex,
            int bobberWaterDepth)
        {
            if (bobberTileIndex < 0)
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("special_catch_sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (specialSource == "fish_frenzy" &&
                    sources.TryGetProperty("fish_frenzy", out var frenzy) &&
                    ReadBool(frenzy, "active") &&
                    string.Equals(ReadString(frenzy, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                    JsonIntArrayContains(frenzy, "eligible_fishable_tile_indices", bobberTileIndex))
                {
                    return true;
                }

                if (specialSource == "base_fallback" &&
                    FishingBaseCatchPathAllows(snapshot, rodSlot, bobberTileIndex) &&
                    sources.TryGetProperty("fallbacks", out var fallbacks) &&
                    context.TryGetProperty("spawn_rules", out var spawnRules) &&
                    spawnRules.TryGetProperty("evaluation_context", out var evaluationContext))
                {
                    var tutorial = ReadBool(evaluationContext, "is_tutorial_catch");
                    var fallbackQualifiedItemId = tutorial
                        ? ReadString(fallbacks, "tutorial_location_data_fallback_qualified_item_id")
                        : ReadString(fallbacks, "no_location_data_match_qualified_item_id");
                    if (string.Equals(fallbackQualifiedItemId, expectedQualifiedItemId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                if (specialSource.StartsWith("fish_pond:", StringComparison.Ordinal) &&
                    sources.TryGetProperty("fish_ponds", out var ponds) && ponds.ValueKind == JsonValueKind.Array &&
                    ponds.EnumerateArray().Any(pond =>
                        ReadBool(pond, "catch_available") &&
                        string.Equals("fish_pond:" + ReadInt(pond, "tile_x") + "," + ReadInt(pond, "tile_y"), specialSource, StringComparison.Ordinal) &&
                        string.Equals(ReadString(pond, "fish_qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                        JsonIntArrayContains(pond, "fishable_tile_indices", bobberTileIndex)))
                {
                    return true;
                }

                if (sources.TryGetProperty("location_get_fish_override", out var locationOverride) &&
                    locationOverride.ValueKind == JsonValueKind.Object &&
                    locationOverride.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in handlers.EnumerateArray())
                    {
                        if (specialSource == "mine_shaft_fishing" &&
                            ReadString(handler, "handler") == "mine_shaft_fishing" &&
                            MineShaftFishingOutputAllows(handler, expectedQualifiedItemId, bobberWaterDepth))
                        {
                            return true;
                        }
                        if (!string.Equals(ReadString(handler, "handler"), specialSource, StringComparison.Ordinal) ||
                            !ReadBool(handler, "eligible_before_catch") ||
                            !string.Equals(ReadString(handler, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        if (!handler.TryGetProperty("fishable_tile_indices", out var indices) ||
                            indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() == 0 ||
                            JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool FishingBaseCatchPathAllows(SnapshotEnvelope snapshot, int rodSlot, int bobberTileIndex)
        {
            if (bobberTileIndex < 0)
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("special_catch_sources", out var sources))
                {
                    continue;
                }

                if (sources.TryGetProperty("location_get_fish_override", out var locationOverride) &&
                    locationOverride.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in handlers.EnumerateArray())
                    {
                        var name = ReadString(handler, "handler");
                        if (name == "mine_shaft_fishing")
                        {
                            if (ReadBool(handler, "uses_training_rod") || ReadInt(handler, "mine_area") == 80)
                            {
                                return false;
                            }
                            continue;
                        }
                        if (name == "island_southeast_stardrop_pool_walnut" &&
                            JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex) &&
                            (ReadBool(handler, "eligible_before_catch") || ReadBool(handler, "matched_pool_without_reward_returns_null")))
                        {
                            return false;
                        }
                        if (ReadBool(handler, "eligible_before_catch") &&
                            (!handler.TryGetProperty("fishable_tile_indices", out var indices) ||
                             indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() == 0 ||
                             JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex)))
                        {
                            return false;
                        }
                    }
                }

                if (sources.TryGetProperty("fish_ponds", out var ponds) && ponds.ValueKind == JsonValueKind.Array &&
                    ponds.EnumerateArray().Any(pond => ReadBool(pond, "catch_available") &&
                        JsonIntArrayContains(pond, "fishable_tile_indices", bobberTileIndex)))
                {
                    return false;
                }
                if (sources.TryGetProperty("fish_frenzy", out var frenzy) && ReadBool(frenzy, "active") &&
                    JsonIntArrayContains(frenzy, "eligible_fishable_tile_indices", bobberTileIndex))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool MineShaftFishingOutputAllows(JsonElement handler, string expectedQualifiedItemId, int waterDepth)
        {
            var usesTrainingRod = ReadBool(handler, "uses_training_rod");
            var mineArea = ReadInt(handler, "mine_area");
            var specialChance = MineShaftSpecialChanceAtDepth(handler, waterDepth);
            if (!usesTrainingRod &&
                string.Equals(ReadString(handler, "special_fish_qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                specialChance > 0d)
            {
                return true;
            }
            if (!usesTrainingRod && mineArea == 80 && expectedQualifiedItemId == "(O)CaveJelly" &&
                specialChance < 1d && ReadDouble(handler, "lava_area_cave_jelly_chance") > 0d)
            {
                return true;
            }
            if ((usesTrainingRod || mineArea == 80) &&
                (usesTrainingRod || specialChance < 1d) &&
                (usesTrainingRod || ReadDouble(handler, "lava_area_cave_jelly_chance") < 1d) &&
                expectedQualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) &&
                int.TryParse(expectedQualifiedItemId.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId) &&
                handler.TryGetProperty("mine_trash_item_id_range_inclusive", out var range) && range.ValueKind == JsonValueKind.Array)
            {
                var values = range.EnumerateArray().Where(value => value.TryGetInt32(out _)).Select(value => value.GetInt32()).ToArray();
                return values.Length == 2 && itemId >= values[0] && itemId <= values[1];
            }
            return false;
        }

        private static double MineShaftSpecialChanceAtDepth(JsonElement handler, int waterDepth)
        {
            if (!handler.TryGetProperty("special_fish_chance_by_water_depth", out var chances) || chances.ValueKind != JsonValueKind.Array)
            {
                return 0d;
            }
            foreach (var chance in chances.EnumerateArray())
            {
                if (ReadInt(chance, "water_depth") == waterDepth)
                {
                    return Math.Clamp(ReadDouble(chance, "special_fish_chance"), 0d, 1d);
                }
            }
            return 0d;
        }

        private static bool FishingRuleHasFixedBlock(JsonElement rule)
        {
            return rule.TryGetProperty("blocking_reasons", out var reasons) && reasons.ValueKind == JsonValueKind.Array &&
                reasons.EnumerateArray().Any(reason =>
                    reason.ValueKind == JsonValueKind.String &&
                    !string.Equals(reason.GetString(), "player_position_mismatch", StringComparison.Ordinal));
        }

        private static bool FishingPlayerRectangleAllows(JsonElement rule, int standX, int standY)
        {
            if (!rule.TryGetProperty("player_position", out var rectangle) || rectangle.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (rectangle.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            var x = ReadInt(rectangle, "x");
            var y = ReadInt(rectangle, "y");
            return standX >= x && standY >= y &&
                standX < x + ReadInt(rectangle, "width") && standY < y + ReadInt(rectangle, "height");
        }

        private static bool JsonIntArrayContains(JsonElement value, string property, int expected)
        {
            return value.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array &&
                items.EnumerateArray().Any(item => item.TryGetInt32(out var number) && number == expected);
        }

        private static int ReadFishingLevel(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "fishing", "location_context");
            return context.HasValue ? ReadInt(context.Value, "fishing_level") : 0;
        }

        private static bool FishingCollisionGridBlocks(SnapshotEnvelope snapshot, int x, int y)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return true;
            }
            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= width || y >= height)
            {
                return true;
            }
            return grid.Value.TryGetProperty("notable_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array &&
                tiles.EnumerateArray().Any(tile =>
                    ReadInt(tile, "tile_x") == x && ReadInt(tile, "tile_y") == y && ReadBool(tile, "collision_blocked"));
        }

        private static bool HasRuntimeShopStockRecheckParameters(SmallModelAction action)
        {
            return !string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(action, "shop_item_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(action, "expected_shop_id")) &&
                ReadIntParameter(action, "quantity") == 1 &&
                ReadIntParameter(action, "max_unit_price").HasValue &&
                string.Equals(ReadParameter(action, "compiler_context.active_menu_type_before_step"), "ShopMenu", StringComparison.Ordinal);
        }

        private static bool DialogueResponseOpensExpectedShop(string? expectedDialogueKey, string? responseKey, string? expectedShopId)
        {
            return (string.Equals(expectedDialogueKey, "Blacksmith", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Blacksmith", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "carpenter", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Carpenter", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "Marnie", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Supplies", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AnimalShop", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "adventureGuild", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AdventureShop", StringComparison.OrdinalIgnoreCase)));
        }

        private static string[] ValidateActiveMenuBracket(SmallModelAction action, SnapshotEnvelope snapshot, OptionSpec? option)
        {
            if (action.OptionId == "executor.close_menu" ||
                action.OptionId == "executor.buy_shop_item" ||
                action.OptionId == "executor.choose_dialogue_response" ||
                option is null)
            {
                return Array.Empty<string>();
            }

            if (option.CompilerResponsibility != CompilerResponsibilities.FullActionExpansion || option.TrainingRole != TrainingRoles.ExecutorCalibration)
            {
                return Array.Empty<string>();
            }

            return ActionSeesActiveMenuOpen(action, snapshot)
                ? new[] { "active_menu_must_be_closed_before_action" }
                : Array.Empty<string>();
        }

        private static string[] ValidateSleepPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.sleep")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (!string.Equals(ReadParameter(action, "compiler_context.is_terminal_step"), "true", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("sleep_action_must_be_terminal");
            }

            if (ActiveMenuOpen(snapshot))
            {
                reasons.Add("sleep_prompt_menu_must_be_clear");
            }

            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            if (prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open"))
            {
                reasons.Add("sleep_confirm_executor_requires_compiler_terminal_macro");
            }

            if (SleepTarget(snapshot) is null)
            {
                reasons.Add("sleep_target_unavailable");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateInteractPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.interact")
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return new[] { "interact_target_tile_required" };
            }

            var interactionKind = ReadParameter(action, "interaction_kind");
            if (string.IsNullOrWhiteSpace(interactionKind))
            {
                return new[] { "interact_kind_required" };
            }

            if (!string.Equals(interactionKind, "map_action", StringComparison.Ordinal))
            {
                return new[] { "interact_kind_unsupported" };
            }

            if (!InteractTargetWithinOneTile(snapshot, targetX.Value, targetY.Value))
            {
                return new[] { "interact_target_not_adjacent" };
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                return new[] { "interact_menu_must_be_clear" };
            }

            if (RouteActionBranchBlockedAtTile(snapshot, targetX.Value, targetY.Value))
            {
                return new[] { "interact_unsupported_action_branch_at_target" };
            }

            var expectedActionType = ReadParameter(action, "expected_action_type");
            if (string.IsNullOrWhiteSpace(expectedActionType))
            {
                return new[] { "interact_expected_action_type_required" };
            }

            if (!TargetActionBranchMatches(snapshot, targetX.Value, targetY.Value, expectedActionType))
            {
                return new[] { "interact_expected_action_type_mismatch" };
            }

            return Array.Empty<string>();
        }

        private static bool InteractTargetWithinOneTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            return playerX.HasValue && playerY.HasValue && Math.Abs(playerX.Value - targetX) + Math.Abs(playerY.Value - targetY) == 1;
        }

        private static bool ActiveMenuOpen(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return false;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                return !string.Equals(activeMenu.Value.GetString(), "none", StringComparison.OrdinalIgnoreCase);
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.Object &&
                activeMenu.Value.TryGetProperty("is_open", out var isOpen))
            {
                return isOpen.ValueKind == JsonValueKind.True;
            }

            return false;
        }

        private static bool ActionSeesActiveMenuOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStep = ReadParameter(action, "compiler_context.active_menu_open_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStep))
            {
                return string.Equals(beforeStep, "true", StringComparison.OrdinalIgnoreCase);
            }

            return ActiveMenuOpen(snapshot);
        }

        private static bool ActionSeesShopMenuOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStepType = ReadParameter(action, "compiler_context.active_menu_type_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStepType))
            {
                return string.Equals(beforeStepType, "ShopMenu", StringComparison.Ordinal);
            }

            return string.Equals(ActiveMenuType(snapshot), "ShopMenu", StringComparison.Ordinal);
        }

        private static bool ActionSeesDialogueBoxOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStepType = ReadParameter(action, "compiler_context.active_menu_type_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStepType))
            {
                return string.Equals(beforeStepType, "DialogueBox", StringComparison.Ordinal);
            }

            return string.Equals(ActiveMenuType(snapshot), "DialogueBox", StringComparison.Ordinal);
        }

        private static string ActiveMenuType(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return string.Empty;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                var value = activeMenu.Value.GetString() ?? string.Empty;
                return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
            }

            return activeMenu.Value.ValueKind == JsonValueKind.Object ? ReadString(activeMenu.Value, "type") : string.Empty;
        }

        private static bool IsSafeCloseMenuType(string type)
        {
            return type is "GameMenu" or "InventoryMenu" or "QuestLog" or "MapPage" or "ProfileMenu" or "ShopMenu";
        }

        private static SleepMacroTarget? SleepTarget(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "home_context");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (ReadBool(context.Value, "home_available") != true || ReadBool(context.Value, "current_location_is_home") != true || ReadBool(context.Value, "bed_tile_has_bed") != true)
            {
                return null;
            }

            var homeLocation = ReadString(context.Value, "home_location_id");
            var bedX = ReadInt(context.Value, "bed_tile_x");
            var bedY = ReadInt(context.Value, "bed_tile_y");
            var stand = FindBestSleepStandTile(snapshot, bedX, bedY);
            if (stand is null)
            {
                return null;
            }

            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x") ?? stand.X;
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y") ?? stand.Y;
            return new SleepMacroTarget
            {
                HomeLocation = string.IsNullOrWhiteSpace(homeLocation) ? "FarmHouse" : homeLocation,
                BedX = bedX,
                BedY = bedY,
                StandX = stand.X,
                StandY = stand.Y,
                FaceDirection = DirectionFromTo(stand.X, stand.Y, bedX, bedY),
                EstimatedTicks = Math.Max(60, (Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y)) * 60)
            };
        }

        private static SleepStandTile? FindBestSleepStandTile(SnapshotEnvelope snapshot, int bedX, int bedY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x") ?? bedX;
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y") ?? bedY;
            return new[]
                {
                    new SleepStandTile(bedX + 1, bedY),
                    new SleepStandTile(bedX - 1, bedY),
                    new SleepStandTile(bedX, bedY + 1),
                    new SleepStandTile(bedX, bedY - 1)
                }
                .Where(tile => SleepStandTileReachable(snapshot, tile.X, tile.Y))
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .FirstOrDefault();
        }

        private static bool SleepStandTileReachable(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0 || !TileInBounds(startX.Value, startY.Value, width, height) || !TileInBounds(targetX, targetY, width, height))
            {
                return false;
            }

            var blocked = ReadBlockedCollisionTiles(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTiles(snapshot);
            return PathExists(startX.Value, startY.Value, targetX, targetY, width, height, blocked, unsupported);
        }

        private static int DirectionFromTo(int fromX, int fromY, int toX, int toY)
        {
            if (toX > fromX)
            {
                return 1;
            }

            if (toX < fromX)
            {
                return 3;
            }

            return toY > fromY ? 2 : 0;
        }

        private static bool RouteActionBranchBlockedAtTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var row = ReadRouteActionBranchRow(snapshot, targetX, targetY);
            return row.HasValue && row.Value.TryGetProperty("route_training_blocked", out var blocked) && blocked.ValueKind == JsonValueKind.True;
        }

        private static bool TargetActionBranchMatches(SnapshotEnvelope snapshot, int targetX, int targetY, string expectedActionType)
        {
            var row = ReadRouteActionBranchRow(snapshot, targetX, targetY);
            return row.HasValue && string.Equals(ReadString(row.Value, "branch"), expectedActionType, StringComparison.OrdinalIgnoreCase);
        }

        private static JsonElement? ReadRouteActionBranchRow(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == targetX &&
                    ReadInt(row, "tile_y") == targetY)
                {
                    return row;
                }
            }

            return null;
        }

        private static string[] ValidateMovementPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.move_to_tile")
            {
                return Array.Empty<string>();
            }

            return ReadIntParameter(action, "target_tile_x").HasValue && ReadIntParameter(action, "target_tile_y").HasValue
                ? Array.Empty<string>()
                : new[] { "movement_target_tile_required" };
        }

        private static string[] ValidateClearObstaclePlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.clear_obstacle")
            {
                return Array.Empty<string>();
            }

            return ReadIntParameter(action, "target_tile_x").HasValue && ReadIntParameter(action, "target_tile_y").HasValue
                ? Array.Empty<string>()
                : new[] { "clear_obstacle_target_tile_required" };
        }

        private static string[] ValidateTillSoilPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.till_soil")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("till_soil_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("till_soil_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("till_soil_target_location_mismatch");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidatePlantSeedPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.plant_seed")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("plant_seed_target_tile_required");
            }

            var seedId = ReadParameter(action, "seed_id");
            if (string.IsNullOrWhiteSpace(seedId))
            {
                seedId = ReadParameter(action, "shop_item_id");
            }

            if (string.IsNullOrWhiteSpace(seedId))
            {
                reasons.Add("plant_seed_seed_id_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("plant_seed_menu_must_be_clear");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !string.IsNullOrWhiteSpace(seedId) &&
                !PlantingContextAllows(snapshot, targetX.Value, targetY.Value, seedId))
            {
                reasons.Add("plant_seed_not_allowed_by_transparent_context");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateHarvestCropPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_crop")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("harvest_crop_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_crop_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_crop_target_location_mismatch");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !HarvestCropReadyAt(snapshot, targetX.Value, targetY.Value))
            {
                reasons.Add("harvest_crop_not_ready_by_transparent_farm_state");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                HarvestCropUsesGrab(action, snapshot, targetX.Value, targetY.Value) &&
                !InventoryMayAcceptHarvestYield(snapshot, targetX.Value, targetY.Value))
            {
                reasons.Add("harvest_crop_inventory_cannot_accept_grab_yield");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateHarvestGiantCropPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_giant_crop")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("harvest_giant_crop_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_giant_crop_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_giant_crop_target_location_mismatch");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !GiantCropResourceClumpAt(snapshot, targetX.Value, targetY.Value).HasValue)
            {
                reasons.Add("harvest_giant_crop_not_verified_by_transparent_resource_clump");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidatePickupDebrisPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.pickup_debris")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("pickup_debris_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("pickup_debris_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("pickup_debris_target_location_mismatch");
            }

            JsonElement? targetDebris = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                targetDebris = DebrisAt(snapshot, targetX.Value, targetY.Value, ReadIntParameter(action, "debris_index"));
                if (!targetDebris.HasValue)
                {
                    reasons.Add("pickup_debris_not_verified_by_transparent_farm_state");
                }
            }

            if (targetDebris.HasValue &&
                !InventoryMayAcceptDebrisItem(snapshot, targetDebris.Value))
            {
                reasons.Add("pickup_debris_inventory_cannot_accept_item");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? DebrisAt(SnapshotEnvelope snapshot, int targetX, int targetY, int? debrisIndex)
        {
            var debris = ReadStateFieldValue(snapshot, "farm", "debris");
            if (!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in debris.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (debrisIndex.HasValue && ReadInt(item, "debris_index") != debrisIndex.Value)
                {
                    continue;
                }

                if (DebrisHasChunkAt(item, targetX, targetY))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool DebrisHasChunkAt(JsonElement debris, int targetX, int targetY)
        {
            if (!debris.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Object &&
                    ReadInt(chunk, "tile_x") == targetX &&
                    ReadInt(chunk, "tile_y") == targetY)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InventoryMayAcceptDebrisItem(SnapshotEnvelope snapshot, JsonElement debris)
        {
            var qualifiedItemId = ReadString(debris, "qualified_item_id");
            var itemId = ReadString(debris, "item_id");
            var normalizedQualifiedId = !string.IsNullOrWhiteSpace(qualifiedItemId)
                ? qualifiedItemId
                : string.IsNullOrWhiteSpace(itemId)
                    ? string.Empty
                    : itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
            if (string.IsNullOrWhiteSpace(normalizedQualifiedId))
            {
                return false;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var quality = ReadInt(debris, "item_quality");
            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), normalizedQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ValidateCollectMachineOutputPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.collect_machine_output")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("collect_machine_output_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("collect_machine_output_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("collect_machine_output_target_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetX.Value, targetY.Value);
                if (!machine.HasValue)
                {
                    reasons.Add("collect_machine_output_not_verified_by_transparent_farm_state");
                }
            }

            if (machine.HasValue)
            {
                if (ReadBool(machine.Value, "ready_for_harvest") != true)
                {
                    reasons.Add("collect_machine_output_not_ready");
                }

                if (!machine.Value.TryGetProperty("held_item", out var heldItem) ||
                    heldItem.ValueKind != JsonValueKind.Object ||
                    string.IsNullOrWhiteSpace(ReadString(heldItem, "qualified_item_id")))
                {
                    reasons.Add("collect_machine_output_item_unavailable");
                }
                else
                {
                    var requestedQualifiedId = ReadParameter(action, "qualified_item_id");
                    if (!string.IsNullOrWhiteSpace(requestedQualifiedId) &&
                        !string.Equals(ReadString(heldItem, "qualified_item_id"), requestedQualifiedId, StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("collect_machine_output_item_mismatch");
                    }

                    if (!InventoryMayAcceptMachineOutput(snapshot, heldItem))
                    {
                        reasons.Add("collect_machine_output_inventory_cannot_accept_item");
                    }
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachineAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var machine in machines.Value.EnumerateArray())
            {
                if (machine.ValueKind == JsonValueKind.Object &&
                    ReadInt(machine, "tile_x") == targetX &&
                    ReadInt(machine, "tile_y") == targetY)
                {
                    return machine;
                }
            }

            return null;
        }

        private static bool InventoryMayAcceptMachineOutput(SnapshotEnvelope snapshot, JsonElement heldItem)
        {
            var qualifiedItemId = ReadString(heldItem, "qualified_item_id");
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return false;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var quality = ReadInt(heldItem, "quality");
            var stack = Math.Max(1, ReadInt(heldItem, "stack"));
            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "maximum_stack_size") - ReadInt(item, "stack") >= stack)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ValidateLoadMachineInputPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.load_machine_input")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var inputSlot = ReadIntParameter(action, "input_slot_index");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("load_machine_input_target_tile_required");
            }

            if (!inputSlot.HasValue)
            {
                reasons.Add("load_machine_input_slot_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("load_machine_input_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("load_machine_input_target_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetX.Value, targetY.Value);
                if (!machine.HasValue)
                {
                    reasons.Add("load_machine_input_not_verified_by_transparent_farm_state");
                }
            }

            if (machine.HasValue)
            {
                if (ReadInt(machine.Value, "minutes_until_ready") > 0 || ReadBool(machine.Value, "ready_for_harvest") == true)
                {
                    reasons.Add("load_machine_input_target_busy");
                }

                JsonElement? input = null;
                if (inputSlot.HasValue)
                {
                    input = MachineLoadableInputAt(machine.Value, inputSlot.Value);
                    if (!input.HasValue)
                    {
                        reasons.Add("load_machine_input_not_verified_by_transparent_probe");
                    }
                }

                if (input.HasValue)
                {
                    var requestedQualifiedId = ReadParameter(action, "qualified_item_id");
                    if (!string.IsNullOrWhiteSpace(requestedQualifiedId) &&
                        !string.Equals(ReadString(input.Value, "qualified_item_id"), requestedQualifiedId, StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("load_machine_input_item_mismatch");
                    }
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachineLoadableInputAt(JsonElement machine, int slotIndex)
        {
            if (!machine.TryGetProperty("loadable_inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var input in inputs.EnumerateArray())
            {
                if (input.ValueKind == JsonValueKind.Object && ReadInt(input, "slot_index") == slotIndex)
                {
                    return input;
                }
            }

            return null;
        }

        private static string[] ValidateConnectorPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.traverse_connector")
            {
                return Array.Empty<string>();
            }

            var errors = new List<string>();
            if (!ReadIntParameter(action, "target_tile_x").HasValue || !ReadIntParameter(action, "target_tile_y").HasValue)
            {
                errors.Add("connector_target_tile_required");
            }

            if (string.IsNullOrWhiteSpace(ReadParameter(action, "expected_target_location")))
            {
                errors.Add("connector_expected_target_location_required");
            }

            var hasArrivalX = ReadIntParameter(action, "expected_arrival_tile_x").HasValue;
            var hasArrivalY = ReadIntParameter(action, "expected_arrival_tile_y").HasValue;
            if (hasArrivalX != hasArrivalY)
            {
                errors.Add("connector_expected_arrival_tile_pair_required");
            }

            return errors.ToArray();
        }

        private static string[] ValidateRouteActionBranches(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue || coverage.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            if (!coverage.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return Array.Empty<string>();
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == targetX.Value &&
                    ReadInt(row, "tile_y") == targetY.Value &&
                    row.TryGetProperty("route_training_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    return new[] { "unsupported_route_action_branch_at_target" };
                }
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateRoutePathPreview(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var startX = ReadIntParameter(action, "start_tile_x") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadIntParameter(action, "start_tile_y") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            if (!targetX.HasValue || !targetY.HasValue || !startX.HasValue || !startY.HasValue)
            {
                return Array.Empty<string>();
            }

            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<string>();
            }

            var blockedTiles = ReadBlockedCollisionTiles(grid.Value);
            var unsupportedTiles = ReadUnsupportedRouteActionTiles(snapshot);

            if (!TileInBounds(startX.Value, startY.Value, width, height) || !TileInBounds(targetX.Value, targetY.Value, width, height))
            {
                return new[] { "route_path_target_out_of_collision_grid" };
            }

            if (blockedTiles.Contains(TileKey(targetX.Value, targetY.Value)))
            {
                return new[] { "route_path_target_blocked_by_collision_grid" };
            }

            if (PathExists(startX.Value, startY.Value, targetX.Value, targetY.Value, width, height, blockedTiles, unsupportedTiles))
            {
                return Array.Empty<string>();
            }

            if (PathExists(startX.Value, startY.Value, targetX.Value, targetY.Value, width, height, blockedTiles, new HashSet<string>(StringComparer.Ordinal)))
            {
                return new[] { "unsupported_route_action_branch_on_path" };
            }

            return new[] { "route_path_blocked_by_collision_grid" };
        }

        private static string[] ValidateRouteGraphPreview(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "exploration.visit_location")
            {
                return Array.Empty<string>();
            }

            var targetLocation = ReadParameter(action, "target_location");
            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (string.IsNullOrWhiteSpace(targetLocation) || string.IsNullOrWhiteSpace(currentLocation) || string.Equals(targetLocation, currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var path = FindResolvedRouteGraphPath(graph.Value, currentLocation, targetLocation);
            if (path.Length == 0)
            {
                return new[] { "route_graph_no_resolved_path" };
            }

            return ValidateRouteGraphStartSegment(action, snapshot, path[0]);
        }

        private static string[] ValidateRouteGraphStartSegment(SmallModelAction action, SnapshotEnvelope snapshot, RouteGraphEdge firstEdge)
        {
            if (!firstEdge.FromX.HasValue || !firstEdge.FromY.HasValue)
            {
                return Array.Empty<string>();
            }

            var startX = ReadIntParameter(action, "start_tile_x") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadIntParameter(action, "start_tile_y") ?? ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<string>();
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<string>();
            }

            if (!TileInBounds(firstEdge.FromX.Value, firstEdge.FromY.Value, width, height))
            {
                return new[] { "route_graph_start_connector_out_of_collision_grid" };
            }

            var blockedTiles = ReadBlockedCollisionTiles(grid.Value);
            if (blockedTiles.Contains(TileKey(firstEdge.FromX.Value, firstEdge.FromY.Value)))
            {
                return new[] { "route_graph_start_connector_blocked_by_collision_grid" };
            }

            var unsupportedTiles = ReadUnsupportedRouteActionTiles(snapshot);
            if (PathExists(startX.Value, startY.Value, firstEdge.FromX.Value, firstEdge.FromY.Value, width, height, blockedTiles, unsupportedTiles))
            {
                return Array.Empty<string>();
            }

            if (PathExists(startX.Value, startY.Value, firstEdge.FromX.Value, firstEdge.FromY.Value, width, height, blockedTiles, new HashSet<string>(StringComparer.Ordinal)))
            {
                return new[] { "unsupported_route_action_branch_on_start_segment" };
            }

            return new[] { "route_graph_start_segment_blocked_by_collision_grid" };
        }

        private sealed class RouteGraphEdge
        {
            public RouteGraphEdge(string fromLocation, string targetLocation, int? fromX, int? fromY)
            {
                FromLocation = fromLocation;
                TargetLocation = targetLocation;
                FromX = fromX;
                FromY = fromY;
            }

            public string FromLocation { get; }
            public string TargetLocation { get; }
            public int? FromX { get; }
            public int? FromY { get; }
        }

        private sealed class SleepStandTile
        {
            public SleepStandTile(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private sealed class SleepMacroTarget
        {
            public string HomeLocation { get; set; } = "FarmHouse";
            public int BedX { get; set; }
            public int BedY { get; set; }
            public int StandX { get; set; }
            public int StandY { get; set; }
            public int FaceDirection { get; set; }
            public int EstimatedTicks { get; set; }
        }

        private static RouteGraphEdge[] FindResolvedRouteGraphPath(JsonElement graph, string startLocation, string targetLocation)
        {
            if (!graph.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<RouteGraphEdge>();
            }

            var adjacency = new Dictionary<string, List<RouteGraphEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges.EnumerateArray())
            {
                if (edge.ValueKind != JsonValueKind.Object ||
                    !edge.TryGetProperty("resolved", out var resolved) ||
                    resolved.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                var from = ReadString(edge, "from_location");
                var target = ReadString(edge, "target_location");
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (!adjacency.TryGetValue(from, out var targets))
                {
                    targets = new List<RouteGraphEdge>();
                    adjacency[from] = targets;
                }

                targets.Add(new RouteGraphEdge(from, target, ReadNullableInt(edge, "from_x"), ReadNullableInt(edge, "from_y")));
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startLocation };
            var queue = new Queue<(string Location, RouteGraphEdge[] Path)>();
            queue.Enqueue((startLocation, Array.Empty<RouteGraphEdge>()));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (string.Equals(current.Location, targetLocation, StringComparison.OrdinalIgnoreCase))
                {
                    return current.Path;
                }

                if (!adjacency.TryGetValue(current.Location, out var nextEdges))
                {
                    continue;
                }

                foreach (var next in nextEdges)
                {
                    if (visited.Add(next.TargetLocation))
                    {
                        queue.Enqueue((next.TargetLocation, current.Path.Concat(new[] { next }).ToArray()));
                    }
                }
            }

            return Array.Empty<RouteGraphEdge>();
        }

        private static HashSet<string> ReadBlockedCollisionTiles(JsonElement collisionGrid)
        {
            var blockedTiles = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty("notable_tiles", out var tiles) || tiles.ValueKind != JsonValueKind.Array)
            {
                return blockedTiles;
            }

            foreach (var tile in tiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object &&
                    tile.TryGetProperty("collision_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    blockedTiles.Add(TileKey(ReadInt(tile, "tile_x"), ReadInt(tile, "tile_y")));
                }
            }

            return blockedTiles;
        }

        private static HashSet<string> ReadUnsupportedRouteActionTiles(SnapshotEnvelope snapshot)
        {
            var unsupportedTiles = new HashSet<string>(StringComparer.Ordinal);
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return unsupportedTiles;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty("route_training_blocked", out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    unsupportedTiles.Add(TileKey(ReadInt(row, "tile_x"), ReadInt(row, "tile_y")));
                }
            }

            return unsupportedTiles;
        }

        private static bool PathExists(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (blockedTiles.Contains(startKey) || blockedTiles.Contains(targetKey) || extraBlockedTiles.Contains(startKey) || extraBlockedTiles.Contains(targetKey))
            {
                return false;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { startKey };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return true;
                }

                foreach (var next in Neighbors(current.X, current.Y))
                {
                    if (!TileInBounds(next.X, next.Y, width, height))
                    {
                        continue;
                    }

                    var key = TileKey(next.X, next.Y);
                    if (visited.Contains(key) || blockedTiles.Contains(key) || extraBlockedTiles.Contains(key))
                    {
                        continue;
                    }

                    visited.Add(key);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static IEnumerable<(int X, int Y)> Neighbors(int x, int y)
        {
            yield return (x + 1, y);
            yield return (x - 1, y);
            yield return (x, y + 1);
            yield return (x, y - 1);
        }

        private static bool TileInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private static string TileKey(int x, int y)
        {
            return x + "," + y;
        }

        private static (string[] BlockingReasons, CandidateDirection? ValidatedDirection) ValidateStrategyPlan(
            SmallModelAction action,
            OptionSpec? option,
            SnapshotEnvelope snapshot,
            string executionMode)
        {
            if (!IsStrategyPlanOption(option, action))
            {
                return (Array.Empty<string>(), null);
            }

            var directionId = ReadParameter(action, "direction_id");
            if (string.IsNullOrWhiteSpace(directionId))
            {
                var blockReason = ReadParameter(action, "block_reason");
                var reason = string.IsNullOrWhiteSpace(blockReason)
                    ? "strategy_direction_id_required"
                    : "strategy_direction_failed_closed:" + blockReason;
                return (new[] { reason }, null);
            }

            if (string.Equals(directionId, "auto_select_best_direction", StringComparison.Ordinal))
            {
                return (new[] { "strategy_auto_select_best_direction_rejected:direction_must_be_selected_by_snapshot_aware_policy" }, null);
            }

            var validationModel = ReadParameter(action, "requires_direction_selection");
            if (string.Equals(validationModel, "failed_no_eligible_candidate", StringComparison.Ordinal))
            {
                return (new[] { "strategy_no_eligible_candidate_available" }, null);
            }

            var goal = ReadParameter(action, "strategic_goal");
            if (goal is null)
            {
                return (new[] { "strategy_strategic_goal_missing:strategic_goal_parameter_required" }, null);
            }
            if (!string.Equals(goal, "grandpa_four_candles_year3", StringComparison.Ordinal))
            {
                return (new[] { "strategy_strategic_goal_invalid:strategic_goal_must_be_grandpa_four_candles_year3_but_was_" + goal }, null);
            }

            var worldModel = new WorldModelProjector().Project(snapshot, goal, executionMode);
            var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
            var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);

            var currentCandidate = sample.CandidateDirections
                .FirstOrDefault(c => string.Equals(c.DirectionId, directionId, StringComparison.Ordinal));

            if (currentCandidate is null)
            {
                return (new[] { "strategy_direction_absent:direction_id_" + directionId + "_not_in_current_snapshot_candidate_set" }, null);
            }

            var reasons = new System.Collections.Generic.List<string>();

            if (!currentCandidate.Known)
            {
                reasons.Add("strategy_direction_not_known:direction_has_unknown_factors_in_current_snapshot");
            }

            if (currentCandidate.Blocked)
            {
                reasons.Add("strategy_direction_blocked:direction_is_blocked_in_current_snapshot");
            }

            if (currentCandidate.PotentialPoints <= 0)
            {
                reasons.Add("strategy_direction_zero_potential:direction_has_no_expected_grandpa_points_gain");
            }

            var modelDomain = ReadParameter(action, "direction_domain") ?? string.Empty;
            if (!string.Equals(modelDomain, currentCandidate.Domain, StringComparison.Ordinal))
            {
                reasons.Add("strategy_direction_domain_mismatch:model=" + modelDomain + ";live=" + currentCandidate.Domain);
            }

            var modelPotential = ReadIntParameter(action, "potential_points");
            if (!modelPotential.HasValue || modelPotential.Value != currentCandidate.PotentialPoints)
            {
                reasons.Add("strategy_potential_points_mismatch:model=" + (modelPotential?.ToString() ?? "null") + ";live=" + currentCandidate.PotentialPoints);
            }

            var modelPriority = ReadDoubleParameter(action, "priority_score");
            if (!modelPriority.HasValue || Math.Abs(modelPriority.Value - currentCandidate.PriorityScore) > 0.0001)
            {
                reasons.Add("strategy_priority_score_mismatch:model=" + (modelPriority?.ToString(CultureInfo.InvariantCulture) ?? "null") + ";live=" + currentCandidate.PriorityScore.ToString(CultureInfo.InvariantCulture));
            }

            var modelFeedbackKey = ReadParameter(action, "feedback_key") ?? string.Empty;
            if (!string.Equals(modelFeedbackKey, currentCandidate.FeedbackKey, StringComparison.Ordinal))
            {
                reasons.Add("strategy_feedback_key_mismatch:model=" + modelFeedbackKey + ";live=" + currentCandidate.FeedbackKey);
            }

            var expectedRequiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(currentCandidate);
            var modelRequiredMinutes = ReadIntParameter(action, "required_minutes");
            if (!modelRequiredMinutes.HasValue || modelRequiredMinutes.Value != expectedRequiredMinutes)
            {
                reasons.Add("strategy_required_minutes_mismatch:model=" + (modelRequiredMinutes?.ToString() ?? "null") + ";live=" + expectedRequiredMinutes);
            }

            var modelOptionalMinutes = ReadIntParameter(action, "optional_minutes");
            if (!modelOptionalMinutes.HasValue)
            {
                reasons.Add("strategy_optional_minutes_missing:optional_minutes_parameter_required");
            }
            else if (modelOptionalMinutes.Value != 0)
            {
                reasons.Add("strategy_optional_minutes_must_be_zero:model=" + modelOptionalMinutes.Value);
            }

            var modelPreconditions = ReadParameter(action, "hard_preconditions");
            if (!string.IsNullOrWhiteSpace(modelPreconditions))
            {
                reasons.Add("strategy_hard_preconditions_not_verifiable:model_value_rejected");
            }

            var modelResourceBudget = ReadParameter(action, "resource_budget");
            if (!string.IsNullOrWhiteSpace(modelResourceBudget))
            {
                reasons.Add("strategy_resource_budget_not_verifiable:model_value_rejected");
            }

            var modelExecutorHandoff = ReadParameter(action, "executor_handoff_option");
            if (!string.IsNullOrWhiteSpace(modelExecutorHandoff))
            {
                reasons.Add("strategy_executor_handoff_not_verifiable:model_value_rejected");
            }

            if (reasons.Count > 0)
            {
                return (reasons.ToArray(), null);
            }

            return (Array.Empty<string>(), currentCandidate);
        }

        private static string[] ValidateSocialPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "social.talk_npc" && action.OptionId != "social.gift_npc")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string> { "social_requires_daily_plan_compilation" };
            var npcName = ReadParameter(action, "npc_name") ?? ReadParameter(action, "target_npc") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_name_required");
            }

            if (action.OptionId == "social.gift_npc")
            {
                if (!ReadIntParameter(action, "slot_index").HasValue)
                {
                    reasons.Add("social_gift_slot_index_required");
                }
                if (string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")))
                {
                    reasons.Add("social_gift_qualified_item_id_required");
                }
            }

            if (!string.IsNullOrWhiteSpace(npcName))
            {
                var candidate = SocialCandidateBuilder.FindMatching(snapshot, action);
                if (candidate is null)
                {
                    reasons.Add("social_current_state_candidate_not_available");
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateSocialInteractPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.social_interact")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var actionKind = ReadParameter(action, "social_action_kind") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_name_required");
            }
            if (actionKind != "talk" && actionKind != "gift")
            {
                reasons.Add("social_action_kind_talk_or_gift_required");
            }
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("social_target_tile_required");
            }
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            if (!standX.HasValue || !standY.HasValue)
            {
                reasons.Add("social_stand_tile_required");
            }
            if (targetX.HasValue && targetY.HasValue && standX.HasValue && standY.HasValue)
            {
                if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
                {
                    reasons.Add("social_stand_not_adjacent_to_npc");
                }
            }
            else
            {
                reasons.Add("social_candidate_stand_npc_evidence_missing");
            }
            if (actionKind == "gift")
            {
                if (!ReadIntParameter(action, "slot_index").HasValue)
                {
                    reasons.Add("social_gift_slot_index_required");
                }
                if (string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")))
                {
                    reasons.Add("social_gift_qualified_item_id_required");
                }
            }
            if (!string.IsNullOrWhiteSpace(npcName) && !string.IsNullOrWhiteSpace(actionKind) &&
                standX.HasValue && standY.HasValue)
            {
                var optionId = actionKind == "gift" ? "social.gift_npc" : "social.talk_npc";
                var probe = new SmallModelAction
                {
                    ActionId = "social.interact.probe",
                    OptionId = optionId,
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "npc_name", Value = npcName }
                    }
                };
                if (actionKind == "gift")
                {
                    var slotIndex = ReadIntParameter(action, "slot_index") ?? 0;
                    var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
                    probe.Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "npc_name", Value = npcName },
                        new SmallModelActionParameter { Name = "slot_index", Value = slotIndex.ToString() },
                        new SmallModelActionParameter { Name = "qualified_item_id", Value = qualifiedItemId }
                    };
                }
                var candidate = SocialCandidateBuilder.FindMatching(snapshot, probe);
                if (candidate is null)
                {
                    reasons.Add("social_current_state_candidate_not_available_for_executor");
                }
                else
                {
                    if (standX.HasValue && standY.HasValue && targetX.HasValue && targetY.HasValue)
                    {
                        var candidateStandXStr = SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_x");
                        var candidateStandYStr = SocialCandidateBuilder.CandidateParameter(candidate, "stand_tile_y");
                        var candidateNpcXStr = SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_x");
                        var candidateNpcYStr = SocialCandidateBuilder.CandidateParameter(candidate, "npc_tile_y");
                        if (!int.TryParse(candidateStandXStr, out var candidateStandX) ||
                            !int.TryParse(candidateStandYStr, out var candidateStandY) ||
                            !int.TryParse(candidateNpcXStr, out var candidateNpcX) ||
                            !int.TryParse(candidateNpcYStr, out var candidateNpcY) ||
                            candidateStandX != standX.Value || candidateStandY != standY.Value ||
                            candidateNpcX != targetX.Value || candidateNpcY != targetY.Value)
                        {
                            reasons.Add("social_candidate_stand_npc_mismatch");
                        }
                    }
                }
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("social_interact_menu_must_be_clear");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool IsStrategyPlanOption(OptionSpec? option, SmallModelAction action)
        {
            return option is not null &&
                option.CompilerResponsibility == CompilerResponsibilities.PlanValidation &&
                action.OptionId == "strategy.grandpa_progress";
        }

        private static StrategyPlanStep[] CompileStrategyPlan(CandidateDirection validatedDirection)
        {
            var requiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(validatedDirection);

            return new[]
            {
                new StrategyPlanStep
                {
                    StepId = "strategy_step." + Guid.NewGuid().ToString("N"),
                    DirectionId = validatedDirection.DirectionId,
                    Domain = validatedDirection.Domain,
                    PotentialPoints = validatedDirection.PotentialPoints,
                    PriorityScore = validatedDirection.PriorityScore,
                    FeedbackKey = validatedDirection.FeedbackKey,
                    RequiredMinutes = requiredMinutes,
                    OptionalMinutes = 0,
                    HardPreconditions = Array.Empty<string>(),
                    ResourceBudget = Array.Empty<string>(),
                    ExecutorHandoffOption = string.Empty
                }
            };
        }

        private static CompiledActionStep[] CompileSteps(SmallModelAction action, SnapshotEnvelope snapshot, OptionSpec? option)
        {
            if (option is null)
            {
                return Array.Empty<CompiledActionStep>();
            }

            if (option.CompilerResponsibility != CompilerResponsibilities.FullActionExpansion)
            {
                return Array.Empty<CompiledActionStep>();
            }

            if (action.OptionId == "farm.maintain_crops")
            {
                return CompileCropMaintenanceSteps(action, snapshot);
            }

            if (action.OptionId == "farm.process_machines")
            {
                return CompileMachineProcessingSteps(snapshot);
            }

            if (action.OptionId == "recovery.stabilize_day")
            {
                return CompileRecoverySteps(snapshot);
            }

            if (action.OptionId == "executor.move_to_tile")
            {
                return CompileMoveToTileStep(action);
            }

            if (action.OptionId == "executor.traverse_connector")
            {
                return CompileTraverseConnectorStep(action);
            }

            if (action.OptionId == "executor.face_direction")
            {
                return CompileFaceDirectionStep(action);
            }

            if (action.OptionId == "executor.interact")
            {
                return CompileInteractStep(action);
            }

            if (action.OptionId == "executor.buy_shop_item")
            {
                return CompileBuyShopItemStep(action, snapshot);
            }

            if (action.OptionId == "executor.choose_dialogue_response")
            {
                return CompileChooseDialogueResponseStep(action);
            }

            if (action.OptionId == "executor.sleep")
            {
                return CompileSleepSteps(snapshot, action);
            }

            if (action.OptionId == "executor.wait_ticks")
            {
                return CompileWaitTicksStep(action);
            }

            if (action.OptionId == "executor.clear_obstacle")
            {
                return CompileClearObstacleStep(action);
            }

            if (action.OptionId == "executor.till_soil")
            {
                return CompileTillSoilStep(action, snapshot);
            }

            if (action.OptionId == "executor.plant_seed")
            {
                return CompilePlantSeedStep(action);
            }

            if (action.OptionId == "executor.harvest_crop")
            {
                return CompileHarvestCropStep(action);
            }

            if (action.OptionId == "executor.harvest_giant_crop")
            {
                return CompileHarvestGiantCropStep(action);
            }

            if (action.OptionId == "executor.pickup_debris")
            {
                return CompilePickupDebrisStep(action);
            }

            if (action.OptionId == "executor.collect_machine_output")
            {
                return CompileCollectMachineOutputStep(action);
            }

            if (action.OptionId == "executor.load_machine_input")
            {
                return CompileLoadMachineInputStep(action);
            }

            if (action.OptionId == "executor.catch_fish")
            {
                return CompileCatchFishStep(action);
            }

            if (action.OptionId == "executor.cool_volcano_lava")
            {
                return CompileCoolVolcanoLavaStep(action);
            }

            if (action.OptionId == "executor.social_interact")
            {
                return CompileSocialInteractStep(action);
            }

            if (action.OptionId == "executor.select_safe_item_slot")
            {
                return CompileSelectSafeItemSlotStep(action, snapshot);
            }

            if (action.OptionId == "executor.close_menu")
            {
                return CompileCloseMenuStep(snapshot);
            }

            return Array.Empty<CompiledActionStep>();
        }

        private static CompiledActionStep[] CompileCloseMenuStep(SnapshotEnvelope snapshot)
        {
            var type = ActiveMenuType(snapshot);
            return new[]
            {
                Step("close_menu", string.IsNullOrWhiteSpace(type) ? "active_menu:none" : "active_menu:" + type, "menus.active_menu.is_open=false", 10)
            };
        }

        private static CompiledActionStep[] CompileCatchFishStep(SmallModelAction action)
        {
            var location = ReadParameter(action, "location_id") ?? ReadParameter(action, "target_location") ?? string.Empty;
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var bobberX = ReadIntParameter(action, "bobber_tile_x");
            var bobberY = ReadIntParameter(action, "bobber_tile_y");
            var rodSlot = ReadIntParameter(action, "rod_slot_index");
            if (string.IsNullOrWhiteSpace(location) || !standX.HasValue || !standY.HasValue ||
                !bobberX.HasValue || !bobberY.HasValue || !rodSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "catch_fish",
                    location + ":stand(" + standX + "," + standY + "):bobber(" + bobberX + "," + bobberY + "):rod_slot=" + rodSlot,
                    "fishing_attempt_completed_with_observed_catch_or_precise_block_reason",
                     Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 30) * 60))
            };
        }

        private static CompiledActionStep[] CompileCoolVolcanoLavaStep(SmallModelAction action)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var wateringCanSlot = ReadIntParameter(action, "watering_can_slot_index");
            if (!targetX.HasValue || !targetY.HasValue || !wateringCanSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "cool_volcano_lava",
                    "target(" + targetX.Value + "," + targetY.Value + "):watering_can_slot=" + wateringCanSlot.Value,
                    "volcano.tiles.cooled_lava_tiles contains target",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static CompiledActionStep[] CompileSocialInteractStep(SmallModelAction action)
        {
            var npcName = ReadParameter(action, "npc_name") ?? string.Empty;
            var actionKind = ReadParameter(action, "social_action_kind") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(actionKind))
            {
                actionKind = actionKind == "talk" || actionKind == "gift" ? actionKind : string.Empty;
            }
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(actionKind) ||
                !targetX.HasValue || !targetY.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var target = "social:" + actionKind + ":" + npcName + ":tile(" + targetX + "," + targetY + ")";
            return new[]
            {
                Step(
                    "social_interact",
                    target,
                    "social_native_execution_attempted_with_observed_outcome",
                    Math.Max(60, (ReadIntParameter(action, "estimated_minutes") ?? 1) * 60))
            };
        }

        private static SocialPlanEnvelope? CompileSocialPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "social.talk_npc" && action.OptionId != "social.gift_npc")
            {
                return null;
            }

            var candidate = SocialCandidateBuilder.FindMatching(snapshot, action);
            var evidence = candidate?.Parameters ?? Array.Empty<SmallModelActionParameter>();
            return new SocialPlanEnvelope
            {
                ActionKind = action.OptionId == "social.talk_npc" ? "talk" : "gift",
                RequestedNpcName = ReadParameter(action, "npc_name") ?? ReadParameter(action, "target_npc") ?? string.Empty,
                RequestedSlotIndex = ReadIntParameter(action, "slot_index"),
                RequestedQualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty,
                LiveLegalityEvidence = evidence
                    .Where(parameter => parameter.Name is "npc_name" or "slot_index" or "qualified_item_id" or "item_quality" or "item_stack_before" or "gift_taste" or "friendship_row_exists_before" or "gift_updates_normal_limits" or "gift_side_effect_risk" or "expected_talked_to_today_before" or "social_legality_evidence")
                    .ToArray(),
                TimeRouteConstraints = evidence
                    .Where(parameter => parameter.Name is "target_location" or "npc_tile_x" or "npc_tile_y" or "stand_tile_x" or "stand_tile_y" or "route_distance_tiles" or "route_distance_ticks" or "native_interaction_planner_budget_ticks")
                    .Concat(new[]
                    {
                        Parameter("duration", "planner_budget_route_distance_plus_native_interaction_ticks"),
                        Parameter("future_schedule_windows", "unavailable_in_this_slice")
                    })
                    .ToArray(),
                ExpectedDeterministicOutcome = evidence
                    .Where(parameter => parameter.Name is "expected_friendship_delta" or "item_stack_before" or "expected_talked_to_today_before")
                    .Concat(new[]
                    {
                        Parameter("result_verified_at_runtime", "true"),
                        Parameter("compiled_primitive_path", "executor.social_interact")
                    })
                    .ToArray(),
                TrainingRecordingContract = SocialTrainingRecordingContract()
            };
        }

        private static string[] SocialTrainingRecordingContract()
        {
            return new[]
            {
                "item_before_after_and_decrement",
                "friendship_points_before_after_delta",
                "talked_and_gift_counters_before_after",
                "dialogue_menu_event_side_effects",
                "npc_and_player_location_tick_time",
                "accepted_rejected_or_blocked_category",
                "primitive_verification",
                "freshness_state_hash",
                "calibration_vs_policy_label"
            };
        }

        private static QuestPlanEnvelope? CompileQuestPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "quest.advance")
            {
                return null;
            }

            var envelope = new QuestPlanEnvelope
            {
                TimeEstimate = "unknown",
                EnergyCost = "unknown",
                ExecutorBlockReason = "quest_native_executor_not_implemented"
            };

            var candidateId = ReadParameter(action, "candidate_id");
            var questId = ReadParameter(action, "quest_id");
            var questKey = ReadParameter(action, "quest_key");
            var runtimeType = ReadParameter(action, "candidate_runtime_type");
            var nextAction = ReadParameter(action, "candidate_next_action");
            var modelTargetNpc = ReadParameter(action, "required_target_npc");
            var modelTargetLocation = ReadParameter(action, "required_target_location");
            var modelItemId = ReadParameter(action, "required_item_id");
            var modelTargetCountStr = ReadParameter(action, "required_target_count");
            var modelCurrentCountStr = ReadParameter(action, "current_progress_count");

            var activeQuests = ReadStateFieldValue(snapshot, "quests", "active_quests");
            var specialOrders = ReadStateFieldValue(snapshot, "quests", "special_orders");

            var rawActiveQuests = activeQuests.HasValue && activeQuests.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<QuestProgressRef[]>(activeQuests.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                : Array.Empty<QuestProgressRef>();

            var rawSpecialOrders = specialOrders.HasValue && specialOrders.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(specialOrders.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();

            var ordinaryCandidates = QuestCandidateBuilder.BuildOrdinaryCandidates(rawActiveQuests);
            var orderCandidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(rawSpecialOrders);
            var allCandidates = ordinaryCandidates.Concat(orderCandidates).ToArray();

            var suppliedIdentities = new List<string>();
            if (!string.IsNullOrWhiteSpace(candidateId)) suppliedIdentities.Add("candidate_id=" + candidateId);
            if (!string.IsNullOrWhiteSpace(questId)) suppliedIdentities.Add("quest_id=" + questId);
            if (!string.IsNullOrWhiteSpace(questKey)) suppliedIdentities.Add("quest_key=" + questKey);

            if (suppliedIdentities.Count == 0)
            {
                envelope.ExecutorBlockReason = "quest_missing_identity";
                return envelope;
            }

            var matchedCandidates = allCandidates.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(candidateId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.CandidateId, candidateId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(questId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestId, questId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(questKey))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestKey, questKey, StringComparison.Ordinal));
            }

            var matchList = matchedCandidates.ToArray();

            if (matchList.Length == 0)
            {
                envelope.ExecutorBlockReason = "quest_candidate_not_found:" + string.Join(";", suppliedIdentities);
                return envelope;
            }

            if (matchList.Length > 1)
            {
                envelope.ExecutorBlockReason = "quest_candidate_ambiguous:" + string.Join(";", suppliedIdentities) + ";matches=" + string.Join(",", matchList.Select(c => c.CandidateId));
                return envelope;
            }

            var match = matchList[0];

            if (!string.IsNullOrWhiteSpace(runtimeType) && !string.Equals(match.RuntimeType, runtimeType, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_runtime_type_mismatch:model=" + runtimeType + ";live=" + match.RuntimeType;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(nextAction) && !string.Equals(match.NextActionCategory, nextAction, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_next_action_mismatch:model=" + nextAction + ";live=" + match.NextActionCategory;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetNpc) && !string.Equals(match.RequiredTargetNpc, modelTargetNpc, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_target_npc_mismatch:model=" + modelTargetNpc + ";live=" + match.RequiredTargetNpc;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetLocation) && !string.Equals(match.RequiredTargetLocation, modelTargetLocation, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_target_location_mismatch:model=" + modelTargetLocation + ";live=" + match.RequiredTargetLocation;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelItemId) && !string.Equals(match.RequiredItemId, modelItemId, StringComparison.Ordinal))
            {
                envelope.ExecutorBlockReason = "quest_item_id_mismatch:model=" + modelItemId + ";live=" + match.RequiredItemId;
                return envelope;
            }

            if (!string.IsNullOrWhiteSpace(modelTargetCountStr))
            {
                if (!int.TryParse(modelTargetCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelTargetCount))
                {
                    envelope.ExecutorBlockReason = "quest_target_count_malformed:value=" + modelTargetCountStr;
                    return envelope;
                }
                if (match.RequiredTargetCount != modelTargetCount)
                {
                    envelope.ExecutorBlockReason = "quest_target_count_mismatch:model=" + modelTargetCount + ";live=" + match.RequiredTargetCount;
                    return envelope;
                }
            }

            if (!string.IsNullOrWhiteSpace(modelCurrentCountStr))
            {
                if (!int.TryParse(modelCurrentCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelCurrentCount))
                {
                    envelope.ExecutorBlockReason = "quest_current_count_malformed:value=" + modelCurrentCountStr;
                    return envelope;
                }
                if (match.CurrentProgressCount != modelCurrentCount)
                {
                    envelope.ExecutorBlockReason = "quest_current_count_mismatch:model=" + modelCurrentCount + ";live=" + match.CurrentProgressCount;
                    return envelope;
                }
            }

            var modelSelectedObjectiveIndexStr = ReadParameter(action, "selected_objective_index");
            if (!string.IsNullOrWhiteSpace(modelSelectedObjectiveIndexStr))
            {
                if (!int.TryParse(modelSelectedObjectiveIndexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelSelectedObjectiveIndex))
                {
                    envelope.ExecutorBlockReason = "quest_selected_objective_index_malformed:value=" + modelSelectedObjectiveIndexStr;
                    return envelope;
                }
                if (match.SelectedObjectiveIndex != modelSelectedObjectiveIndex)
                {
                    envelope.ExecutorBlockReason = "quest_selected_objective_index_mismatch:model=" + modelSelectedObjectiveIndex + ";live=" + match.SelectedObjectiveIndex;
                    return envelope;
                }
            }

            envelope.SelectedCandidateId = match.CandidateId;
            envelope.SelectedQuestId = match.QuestId;
            envelope.SelectedQuestKey = match.QuestKey;
            envelope.SelectedRuntimeType = match.RuntimeType;
            envelope.Family = match.Family;
            envelope.NextActionCategory = match.NextActionCategory;
            envelope.RequiredTargetNpc = match.RequiredTargetNpc;
            envelope.RequiredTargetLocation = match.RequiredTargetLocation;
            envelope.RequiredItemId = match.RequiredItemId;
            envelope.RequiredTargetCount = match.RequiredTargetCount;
            envelope.CurrentProgressCount = match.CurrentProgressCount;
            envelope.SelectedObjectiveIndex = match.SelectedObjectiveIndex;
            envelope.LiveEvidence = new QuestCompilerEvidence
            {
                Candidate = match,
                RawActiveQuests = rawActiveQuests,
                RawSpecialOrders = rawSpecialOrders
            };

            return envelope;
        }

        private static CompiledActionStep[] CompileSelectSafeItemSlotStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var safeSlot = ReadIntParameter(action, "safe_slot_index") ?? SafeSlotIndex(snapshot);
            if (!safeSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("select_safe_item_slot", safeSlot.Value.ToString(), "player.current_tool_index=" + safeSlot.Value + ";player.active_object_qualified_id=null", 10)
            };
        }

        private static CompiledActionStep[] CompileMoveToTileStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var location = ReadParameter(action, "target_location") ?? "current_location";
            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
            return new[]
            {
                Step("move_to_tile", location + "(" + x.Value + "," + y.Value + ")", "player_reaches_target_tile_or_blocked", estimatedTicks)
            };
        }

        private static CompiledActionStep[] CompileClearObstacleStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var maxSwings = Math.Clamp(ReadIntParameter(action, "max_tool_swings") ?? 8, 1, 64);
            return new[]
            {
                Step(
                    "clear_obstacle",
                    "current_location(" + x.Value + "," + y.Value + ")",
                    "current_location.obstacle[" + x.Value + "," + y.Value + "]=clear_or_blocked",
                    maxSwings * 60)
            };
        }

        private static CompiledActionStep[] CompileTillSoilStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "till_soil",
                    "Farm(" + x.Value + "," + y.Value + ")",
                    "farm.terrain_features[" + x.Value + "," + y.Value + "].type=HoeDirt;native_tool=Hoe",
                    EstimateToolActionTicks(snapshot, x.Value, y.Value))
            };
        }

        private static CompiledActionStep[] CompilePlantSeedStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var seedId = ReadParameter(action, "seed_id") ?? ReadParameter(action, "shop_item_id") ?? string.Empty;
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(seedId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "plant_seed",
                    "current_location(" + x.Value + "," + y.Value + "):" + seedId,
                    "current_location.planting_context[" + x.Value + "," + y.Value + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases",
                    60)
            };
        }

        private static CompiledActionStep[] CompileHarvestCropStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var harvestMethod = ReadParameter(action, "harvest_method") ?? "unknown";
            return new[]
            {
                Step(
                    "harvest_crop",
                    "Farm(" + x.Value + "," + y.Value + "):" + harvestMethod,
                    "farm.crops[" + x.Value + "," + y.Value + "].ready_for_harvest=false_or_blocked",
                    60)
            };
        }

        private static CompiledActionStep[] CompileHarvestGiantCropStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var maxSwings = Math.Clamp(ReadIntParameter(action, "max_tool_swings") ?? 16, 1, 64);
            return new[]
            {
                Step(
                    "harvest_giant_crop",
                    "Farm(" + x.Value + "," + y.Value + "):axe",
                    "farm.resource_clumps[" + x.Value + "," + y.Value + "].is_giant_crop=false_or_blocked",
                    maxSwings * 60)
            };
        }

        private static CompiledActionStep[] CompilePickupDebrisStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var debrisIndex = ReadIntParameter(action, "debris_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            return new[]
            {
                Step(
                    "pickup_debris",
                    "Farm(" + x.Value + "," + y.Value + "):" + (debrisIndex.HasValue ? "debris_index=" + debrisIndex.Value : qualifiedItemId),
                    "farm.debris[" + (debrisIndex.HasValue ? debrisIndex.Value.ToString() : x.Value + "," + y.Value) + "].chunk_count_decreases_or_removed=true;player.inventory.updated",
                    30)
            };
        }

        private static CompiledActionStep[] CompileCollectMachineOutputStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var expectedEffect = "farm.machines[" + x.Value + "," + y.Value + "].held_item=null;player.inventory.updated";
            expectedEffect += OptionalEffect(action, "qualified_item_id");
            expectedEffect += OptionalEffect(action, "item_id");
            expectedEffect += OptionalEffect(action, "output_stack");
            expectedEffect += OptionalEffect(action, "output_sale_price");
            expectedEffect += OptionalEffect(action, "output_total_value");
            expectedEffect += OptionalEffect(action, "machine_value_basis");
            return new[]
            {
                Step(
                    "collect_machine_output",
                    "Farm(" + x.Value + "," + y.Value + "):" + qualifiedItemId,
                    expectedEffect,
                    30)
            };
        }

        private static CompiledActionStep[] CompileLoadMachineInputStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var inputSlot = ReadIntParameter(action, "input_slot_index");
            if (!x.HasValue || !y.HasValue || !inputSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var expectedEffect = "farm.machines[" + x.Value + "," + y.Value + "].minutes_until_ready>0_or_ready=true;player.inventory[" + inputSlot.Value + "].stack_decreases";
            expectedEffect += OptionalEffect(action, "input_slot_index");
            expectedEffect += OptionalEffect(action, "qualified_item_id");
            expectedEffect += OptionalEffect(action, "item_id");
            expectedEffect += OptionalEffect(action, "input_stack_available");
            expectedEffect += OptionalEffect(action, "predicted_output_qualified_item_id");
            expectedEffect += OptionalEffect(action, "predicted_output_item_id");
            expectedEffect += OptionalEffect(action, "predicted_output_stack");
            expectedEffect += OptionalEffect(action, "predicted_output_sale_price");
            expectedEffect += OptionalEffect(action, "predicted_output_total_value");
            expectedEffect += OptionalEffect(action, "predicted_output_net_value");
            expectedEffect += OptionalEffect(action, "predicted_minutes_until_ready");
            return new[]
            {
                Step(
                    "load_machine_input",
                    "Farm(" + x.Value + "," + y.Value + "):slot" + inputSlot.Value + ":" + qualifiedItemId,
                    expectedEffect,
                    30)
            };
        }

        private static string OptionalEffect(SmallModelAction action, string parameterName)
        {
            var value = ReadParameter(action, parameterName);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : ";" + parameterName + "=" + value;
        }

        private static CompiledActionStep[] CompileTraverseConnectorStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var targetLocation = ReadParameter(action, "expected_target_location");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(targetLocation))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var arrivalX = ReadIntParameter(action, "expected_arrival_tile_x");
            var arrivalY = ReadIntParameter(action, "expected_arrival_tile_y");
            var expected = "location=" + targetLocation;
            if (arrivalX.HasValue && arrivalY.HasValue)
            {
                expected += ";player.tile=" + arrivalX.Value + "," + arrivalY.Value;
            }

            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
            return new[]
            {
                Step("traverse_connector", "current_location(" + x.Value + "," + y.Value + ")", expected, estimatedTicks)
            };
        }

        private static CompiledActionStep[] CompileFaceDirectionStep(SmallModelAction action)
        {
            var direction = ReadIntParameter(action, "direction");
            if (!direction.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("face_direction", direction.Value.ToString(), "player_facing_direction_changed", 1)
            };
        }

        private static CompiledActionStep[] CompileWaitTicksStep(SmallModelAction action)
        {
            var waitTicks = ReadIntParameter(action, "wait_ticks");
            if (!waitTicks.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("wait_ticks", waitTicks.Value.ToString(), "ticks_elapsed_without_mutation", waitTicks.Value)
            };
        }

        private static CompiledActionStep[] CompileSleepSteps(SnapshotEnvelope snapshot, SmallModelAction? action = null)
        {
            if (action is null ? ActiveMenuOpen(snapshot) : ActionSeesActiveMenuOpen(action, snapshot))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var target = SleepTarget(snapshot);
            if (target is null)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("move_to_bed_adjacent", target.HomeLocation + "(" + target.StandX + "," + target.StandY + ")", "player.tile=" + target.StandX + "," + target.StandY, target.EstimatedTicks),
                Step("step_onto_sleep_touch_tile", target.HomeLocation + "(" + target.BedX + "," + target.BedY + ")", "TouchAction=Sleep;menus.sleep_prompt_context.prompt_open=true", 30),
                Step("confirm_sleep_yes", "menus.sleep_prompt_context", "day_safely_ended", 120)
            };
        }

        private static CompiledActionStep[] CompileInteractStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var expectedActionType = ReadParameter(action, "expected_action_type") ?? "unknown";
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("interact", "current_location(" + x.Value + "," + y.Value + ")", "interact_map_action_" + expectedActionType, 30)
            };
        }

        private static CompiledActionStep[] CompileBuyShopItemStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var normalized = BuildBuyShopItemParameters(action, snapshot);
            var qualifiedItemId = normalized.FirstOrDefault(item => item.Name == "qualified_item_id")?.Value ?? string.Empty;
            var quantity = normalized.FirstOrDefault(item => item.Name == "quantity")?.Value ?? "1";
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("buy_shop_item", qualifiedItemId + "x" + quantity, "player.inventory_count_increases;player.money_decreases", 20)
            };
        }

        private static CompiledActionStep[] CompileChooseDialogueResponseStep(SmallModelAction action)
        {
            var expectedDialogueKey = ReadParameter(action, "expected_dialogue_key") ?? string.Empty;
            var responseKey = ReadParameter(action, "dialogue_response_key") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expectedDialogueKey) || string.IsNullOrWhiteSpace(responseKey))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("choose_dialogue_response", expectedDialogueKey + ":" + responseKey, "expected_dialogue_response_effect", 20)
            };
        }

        private static CompiledActionStep[] CompileCropMaintenanceSteps(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("farm", out var farm) ||
                farm.ValueKind != JsonValueKind.Object ||
                !farm.TryGetProperty("crops", out var cropsField) ||
                !cropsField.TryGetProperty("value", out var crops) ||
                crops.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var hasTargetTile = targetX.HasValue && targetY.HasValue;
            var targetXValue = targetX.GetValueOrDefault();
            var targetYValue = targetY.GetValueOrDefault();
            var maxCrops = ReadIntParameter(action, "max_crops");
            var limit = maxCrops.GetValueOrDefault(int.MaxValue);
            var steps = new List<CompiledActionStep>();
            foreach (var crop in crops.EnumerateArray())
            {
                if (steps.Count >= limit)
                {
                    break;
                }

                if (crop.ValueKind != JsonValueKind.Object ||
                    !crop.TryGetProperty("needs_watering", out var needsWatering) ||
                    needsWatering.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                var x = ReadInt(crop, "tile_x");
                var y = ReadInt(crop, "tile_y");
                if (hasTargetTile && (x != targetXValue || y != targetYValue))
                {
                    continue;
                }

                steps.Add(new CompiledActionStep
                {
                    StepId = "step." + Guid.NewGuid().ToString("N"),
                    StepType = "water_crop",
                    Target = "Farm(" + x + "," + y + ")",
                    ExpectedEffect = "farm.crops[" + x + "," + y + "].needs_watering=false;native_tool=WateringCan",
                    EstimatedTicks = EstimateToolActionTicks(snapshot, x, y)
                });
            }

            if (steps.Count == 0)
            {
                steps.Add(new CompiledActionStep
                {
                    StepId = "step." + Guid.NewGuid().ToString("N"),
                    StepType = "crop_maintenance_noop",
                    Target = "Farm",
                    ExpectedEffect = "no_crop_needs_watering",
                    EstimatedTicks = 0
                });
            }

            return steps.ToArray();
        }

        private static int EstimateToolActionTicks(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var routeTicks = playerX.HasValue && playerY.HasValue
                ? Math.Max(0, Math.Abs(playerX.Value - targetX) + Math.Abs(playerY.Value - targetY) - 1) * 30
                : 30;
            return routeTicks + 5 + 60 + 20;
        }

        private static int CountCrops(SnapshotEnvelope snapshot)
        {
            return ReadCropArray(snapshot)?.GetArrayLength() ?? 0;
        }

        private static int CountWateringCandidates(SnapshotEnvelope snapshot)
        {
            var crops = ReadCropArray(snapshot);
            if (!crops.HasValue)
            {
                return 0;
            }

            var count = 0;
            foreach (var crop in crops.Value.EnumerateArray())
            {
                if (crop.ValueKind == JsonValueKind.Object &&
                    crop.TryGetProperty("needs_watering", out var needsWatering) &&
                    needsWatering.ValueKind == JsonValueKind.True)
                {
                    count++;
                }
            }

            return count;
        }

        private static JsonElement? ReadCropArray(SnapshotEnvelope snapshot)
        {
            return snapshot.State.TryGetValue("farm", out var farm) &&
                farm.ValueKind == JsonValueKind.Object &&
                farm.TryGetProperty("crops", out var cropsField) &&
                cropsField.TryGetProperty("value", out var crops) &&
                crops.ValueKind == JsonValueKind.Array
                ? crops
                : null;
        }

        private static bool PlantingContextAllows(SnapshotEnvelope snapshot, int targetX, int targetY, string seedId)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "planting_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("hoe_dirt_tiles", out var tiles) ||
                tiles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var tile in tiles.EnumerateArray())
            {
                if (tile.ValueKind != JsonValueKind.Object ||
                    ReadInt(tile, "tile_x") != targetX ||
                    ReadInt(tile, "tile_y") != targetY ||
                    ReadBool(tile, "has_crop") == true ||
                    !tile.TryGetProperty("seed_results", out var seedResults) ||
                    seedResults.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in seedResults.EnumerateArray())
                {
                    if (result.ValueKind == JsonValueKind.Object &&
                        string.Equals(ReadString(result, "seed_id"), seedId, StringComparison.OrdinalIgnoreCase) &&
                        ReadBool(result, "hard_rule_allows_planting") == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HarvestCropReadyAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crop = HarvestCropAt(snapshot, targetX, targetY);
            return crop.HasValue && ReadBool(crop.Value, "ready_for_harvest") == true;
        }

        private static bool HarvestCropUsesGrab(SmallModelAction action, SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var actionMethod = ReadParameter(action, "harvest_method");
            if (!string.IsNullOrWhiteSpace(actionMethod))
            {
                return string.Equals(actionMethod, "Grab", StringComparison.OrdinalIgnoreCase);
            }

            var crop = HarvestCropAt(snapshot, targetX, targetY);
            return crop.HasValue &&
                string.Equals(ReadString(crop.Value, "harvest_method"), "Grab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool InventoryMayAcceptHarvestYield(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crop = HarvestCropAt(snapshot, targetX, targetY);
            if (!crop.HasValue)
            {
                return false;
            }

            var harvestItemId = ReadString(crop.Value, "harvest_item_id");
            if (string.IsNullOrWhiteSpace(harvestItemId))
            {
                return true;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var qualifiedHarvestId = harvestItemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                ? harvestItemId
                : "(O)" + harvestItemId;
            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), qualifiedHarvestId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == 0 &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonElement? HarvestCropAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crops = ReadCropArray(snapshot);
            if (!crops.HasValue)
            {
                return null;
            }

            foreach (var crop in crops.Value.EnumerateArray())
            {
                if (crop.ValueKind == JsonValueKind.Object &&
                    ReadInt(crop, "tile_x") == targetX &&
                    ReadInt(crop, "tile_y") == targetY)
                {
                    return crop;
                }
            }

            return null;
        }

        private static JsonElement? GiantCropResourceClumpAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
            if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var clump in clumps.Value.EnumerateArray())
            {
                if (clump.ValueKind != JsonValueKind.Object ||
                    ReadBool(clump, "is_giant_crop") != true)
                {
                    continue;
                }

                var x = ReadInt(clump, "tile_x");
                var y = ReadInt(clump, "tile_y");
                var width = Math.Max(1, ReadInt(clump, "width"));
                var height = Math.Max(1, ReadInt(clump, "height"));
                if (targetX >= x && targetX < x + width &&
                    targetY >= y && targetY < y + height)
                {
                    return clump;
                }
            }

            return null;
        }

        private static CompiledActionStep[] CompileMachineProcessingSteps(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("farm", out var farm) ||
                farm.ValueKind != JsonValueKind.Object ||
                !farm.TryGetProperty("machines", out var machinesField) ||
                !machinesField.TryGetProperty("value", out var machines) ||
                machines.ValueKind != JsonValueKind.Array)
            {
                return new[]
                {
                    Step("machine_processing_noop", "Farm", "no_machine_data_available", 0)
                };
            }

            var steps = new List<CompiledActionStep>();
            foreach (var machine in machines.EnumerateArray())
            {
                if (machine.ValueKind != JsonValueKind.Object || !IsMachineReady(machine))
                {
                    continue;
                }

                var x = ReadInt(machine, "tile_x");
                var y = ReadInt(machine, "tile_y");
                steps.Add(Step("process_machine", "Farm(" + x + "," + y + ")", "machine_output_collected_or_input_loaded", 80));
            }

            return steps.Count == 0
                ? new[] { Step("machine_processing_noop", "Farm", "no_machine_ready", 0) }
                : steps.ToArray();
        }

        private static CompiledActionStep[] CompileRecoverySteps(SnapshotEnvelope snapshot)
        {
            if (ActiveMenuOpen(snapshot))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var steps = new List<CompiledActionStep>();

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (time >= 2400)
            {
                steps.AddRange(CompileSleepSteps(snapshot));
            }
            else if (time >= 2200)
            {
                steps.AddRange(CompileSleepSteps(snapshot));
            }
            else
            {
                steps.Add(Step("refresh_plan_after_stabilization", "planner", "urgent_risks_rechecked", 0));
            }

            return steps.ToArray();
        }

        private static bool IsMachineReady(JsonElement machine)
        {
            foreach (var property in new[] { "ready", "ready_for_harvest", "has_output", "needs_processing" })
            {
                if (machine.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ReadStateFieldInt(SnapshotEnvelope snapshot, string section, string property)
        {
            return ReadStateFieldIntOptional(snapshot, section, property) ?? 0;
        }

        private static int? ReadStateFieldIntOptional(SnapshotEnvelope snapshot, string section, string property)
        {
            return snapshot.State.TryGetValue(section, out var sectionValue) &&
                sectionValue.ValueKind == JsonValueKind.Object &&
                sectionValue.TryGetProperty(property, out var field) &&
                field.TryGetProperty("value", out var value) &&
                value.TryGetInt32(out var result)
                ? result
                : null;
        }

        private static double? ReadStateFieldDoubleOptional(SnapshotEnvelope snapshot, string section, string property)
        {
            return snapshot.State.TryGetValue(section, out var sectionValue) &&
                sectionValue.ValueKind == JsonValueKind.Object &&
                sectionValue.TryGetProperty(property, out var field) &&
                field.TryGetProperty("value", out var value) &&
                value.TryGetDouble(out var result)
                ? result
                : null;
        }

        private static string ReadStateFieldString(SnapshotEnvelope snapshot, string section, string property)
        {
            var value = ReadStateFieldValue(snapshot, section, property);
            return value.HasValue &&
                value.Value.ValueKind == JsonValueKind.String
                ? value.Value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static JsonElement? ReadStateFieldValue(SnapshotEnvelope snapshot, string section, string property)
        {
            return snapshot.State.TryGetValue(section, out var sectionValue) &&
                sectionValue.ValueKind == JsonValueKind.Object &&
                sectionValue.TryGetProperty(property, out var field) &&
                field.TryGetProperty("value", out var value)
                ? value
                : null;
        }

        private static int? SafeSlotIndex(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "player", "safe_item_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !ReadBool(context.Value, "safe_slot_available"))
            {
                return null;
            }

            return ReadNullableInt(context.Value, "safe_slot_index");
        }

        private static CompiledActionStep Step(string stepType, string target, string expectedEffect, int estimatedTicks)
        {
            return new CompiledActionStep
            {
                StepId = "step." + Guid.NewGuid().ToString("N"),
                StepType = stepType,
                Target = target,
                ExpectedEffect = expectedEffect,
                EstimatedTicks = estimatedTicks
            };
        }

        private static int? ReadIntParameter(SmallModelAction action, string name)
        {
            var value = ReadParameter(action, name);
            return int.TryParse(value, out var result) ? result : null;
        }

        private static double? ReadDoubleParameter(SmallModelAction action, string name)
        {
            var value = ReadParameter(action, name);
            return double.TryParse(value, out var result) ? result : null;
        }

        private static string? ReadParameter(SmallModelAction action, string name)
        {
            return action.Parameters
                .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
                ?.Value;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value ?? string.Empty
            };
        }

        private static string[] SplitList(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => item.Length > 0)
                    .ToArray();
        }

        private static int ReadInt(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
                ? result
                : 0;
        }

        private static string ReadString(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool ReadBool(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
        }

        private static double ReadDouble(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.TryGetDouble(out var result)
                ? result
                : 0d;
        }

        private static int? ReadNullableInt(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
                ? result
                : null;
        }
    }
}
