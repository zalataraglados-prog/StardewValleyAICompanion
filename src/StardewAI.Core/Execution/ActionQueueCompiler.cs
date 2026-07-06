using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Verifier;

namespace StardewAI.Core.Execution
{
    public sealed class ActionQueueCompiler
    {
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

            var status = blocking.Count == 0 && safety.Feasibility == "feasible"
                ? "pending"
                : "blocked";

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
                        : "option_request",
                    OptionId = action.OptionId,
                    BehaviorCategory = option?.BehaviorCategory ?? OptionBehaviorCategories.Unknown,
                    CompilerResponsibility = option?.CompilerResponsibility ?? CompilerResponsibilities.Unknown,
                    TrainingRole = option?.TrainingRole ?? TrainingRoles.Unknown,
                    StateHash = snapshot.StateHash,
                    ExecutionMode = executionMode,
                    Actor = actor,
                    Parameters = action.Parameters,
                    Steps = CompileSteps(action, snapshot, option)
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
                return CompileCropMaintenanceSteps(snapshot, ReadIntParameter(action, "max_crops"));
            }

            if (action.OptionId == "farm.process_machines")
            {
                return CompileMachineProcessingSteps(snapshot);
            }

            if (action.OptionId == "recovery.stabilize_day")
            {
                return CompileRecoverySteps(snapshot);
            }

            return Array.Empty<CompiledActionStep>();
        }

        private static CompiledActionStep[] CompileCropMaintenanceSteps(SnapshotEnvelope snapshot, int? maxCrops)
        {
            if (!snapshot.State.TryGetValue("farm", out var farm) ||
                farm.ValueKind != JsonValueKind.Object ||
                !farm.TryGetProperty("crops", out var cropsField) ||
                !cropsField.TryGetProperty("value", out var crops) ||
                crops.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CompiledActionStep>();
            }

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
                steps.Add(new CompiledActionStep
                {
                    StepId = "step." + Guid.NewGuid().ToString("N"),
                    StepType = "water_crop",
                    Target = "Farm(" + x + "," + y + ")",
                    ExpectedEffect = "crop_watered",
                    EstimatedTicks = 60
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
            var steps = new List<CompiledActionStep>
            {
                Step("close_blocking_menu", "active_menu", "menu_not_blocking_execution", 10)
            };

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (time >= 2400)
            {
                steps.Add(Step("sleep_immediately", "farmhouse_bed", "day_safely_ended", 120));
            }
            else if (time >= 2200)
            {
                steps.Add(Step("return_home", "farmhouse", "player_in_safe_sleep_route", 900));
                steps.Add(Step("sleep_before_collapse", "farmhouse_bed", "day_safely_ended", 120));
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
            return snapshot.State.TryGetValue(section, out var sectionValue) &&
                sectionValue.ValueKind == JsonValueKind.Object &&
                sectionValue.TryGetProperty(property, out var field) &&
                field.TryGetProperty("value", out var value) &&
                value.TryGetInt32(out var result)
                ? result
                : 0;
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
            var value = action.Parameters.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))?.Value;
            return int.TryParse(value, out var result) ? result : null;
        }

        private static int ReadInt(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
                ? result
                : 0;
        }
    }
}
