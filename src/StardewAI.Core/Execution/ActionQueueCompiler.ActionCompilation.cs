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
            blocking.AddRange(ValidateClearObstaclePlan(action, snapshot));
            blocking.AddRange(ValidateFarmResourceClumpPlan(action, snapshot));
            blocking.AddRange(ValidateTillSoilPlan(action, snapshot));
            blocking.AddRange(ValidatePlantSeedPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestCropPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestGiantCropPlan(action, snapshot));
            blocking.AddRange(ValidatePickupDebrisPlan(action, snapshot));
            blocking.AddRange(ValidateCollectSpawnedObjectPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestGingerPlan(action, snapshot));
            blocking.AddRange(ValidateHarvestBushPlan(action, snapshot));
            blocking.AddRange(ValidateMineRewardChestPlan(action, snapshot));
            blocking.AddRange(ValidateCollectCrabPotPlan(action, snapshot));
            blocking.AddRange(ValidateFishPondPlan(action, snapshot));
            blocking.AddRange(ValidateCollectAnimalProductPlan(action, snapshot));
            blocking.AddRange(ValidatePanOreSpotPlan(action, snapshot));
            blocking.AddRange(ValidateCollectMachineOutputPlan(action, snapshot));
            blocking.AddRange(ValidateLoadMachineInputPlan(action, snapshot));
            blocking.AddRange(ValidateConnectorPlan(action, snapshot));
            blocking.AddRange(ValidateFaceDirectionPlan(action));
            blocking.AddRange(ValidateInteractPlan(action, snapshot));
            blocking.AddRange(ValidateSleepPlan(action, snapshot));
            blocking.AddRange(ValidateWaitTicksPlan(action));
            blocking.AddRange(ValidateCatchFishPlan(action, snapshot));
            blocking.AddRange(ValidateMiningReachDepthPlan(action, snapshot));
            blocking.AddRange(ValidateMiningSkullKeyPlan(action, snapshot));
            blocking.AddRange(ValidateMiningGoldenScythePlan(action, snapshot));
            blocking.AddRange(ValidateVolcanoReachCalderaPlan(action, snapshot));
            blocking.AddRange(ValidateCoolVolcanoLavaPlan(action, snapshot));
            blocking.AddRange(ValidateVolcanoNativePrimitivePlan(action, snapshot));
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

    }
}
