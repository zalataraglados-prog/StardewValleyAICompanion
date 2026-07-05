using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
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

            diagnostics.AddRange(ValidateActor(modelOutput.Actor));

            var items = modelOutput.Actions
                .Select(action => CompileAction(action, snapshot, modelOutput.Actor, diagnostics.Count > 0))
                .ToArray();
            var blocked = diagnostics.Count > 0 || items.Any(item => item.Status == "blocked");

            return new ActionQueueEnvelope
            {
                QueueId = "queue." + Guid.NewGuid().ToString("N"),
                SourceModelOutputId = modelOutput.ModelOutputId,
                SourceModel = modelOutput.SourceModel,
                StateHash = snapshot.StateHash,
                GoalId = modelOutput.GoalId,
                Actor = modelOutput.Actor,
                Status = blocked ? "blocked" : "pending",
                Items = items,
                CompilerDiagnostics = diagnostics.ToArray()
            };
        }

        private static string[] ValidateActor(ActionActorRef actor)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(actor.ActorId))
            {
                errors.Add("actor_id_required");
            }

            if (!string.Equals(actor.ActorType, "ai_companion", StringComparison.Ordinal))
            {
                errors.Add("actor_type_must_be_ai_companion");
            }

            if (!string.Equals(actor.ControlSurface, "companion_actor", StringComparison.Ordinal))
            {
                errors.Add("control_surface_must_be_companion_actor");
            }

            return errors.ToArray();
        }

        private ActionQueueItem CompileAction(SmallModelAction action, SnapshotEnvelope snapshot, ActionActorRef actor, bool globallyBlocked)
        {
            var blocking = new List<string>();
            SafetyResult safety;
            string[] requiredFactors;
            try
            {
                var option = optionRegistry.GetRequired(action.OptionId);
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
                    OptionId = action.OptionId,
                    StateHash = snapshot.StateHash,
                    Actor = actor,
                    Parameters = action.Parameters
                }
            };
        }
    }
}
