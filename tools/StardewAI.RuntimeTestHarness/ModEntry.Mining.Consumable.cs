using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartConsumeFood(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "inventory[" + (request.SlotIndex?.ToString() ?? "missing") + "].stack-=1;player.health>before;native_dialogue=Eat_Yes";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), reasons.ToArray()));
            return;
        }

        if (activeConsumeFood is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_executor_busy"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_active_menu_must_be_closed"));
            return;
        }
        if (!request.SlotIndex.HasValue || request.SlotIndex.Value < 0 || request.SlotIndex.Value >= Game1.player.Items.Count)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_slot_out_of_range"));
            return;
        }

        var slotIndex = request.SlotIndex.Value;
        if (Game1.player.Items[slotIndex] is not StardewValley.Object food || food.Edibility <= 0 || food.healthRecoveredOnConsumption() <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_slot_not_healing_food"));
            return;
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId) || !string.Equals(food.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_item_identity_mismatch"));
            return;
        }
        if (Game1.player.health >= Game1.player.maxHealth)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_health_already_full"));
            return;
        }
        if (Game1.player.hasBuff("25") && !food.HasContextTag("ginger_item"))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_nauseous"));
            return;
        }
        if (Game1.player.hasBuff("6"))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_food_fullness_active"));
            return;
        }
        if (Game1.player.team.SpecialOrderRuleActive("SC_NO_FOOD") && mine.getMineArea() == 121)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_special_order_forbids_food"));
            return;
        }

        activeConsumeFood = new ActiveConsumeFood(
            pending,
            mine.NameOrUniqueName,
            slotIndex,
            food.QualifiedItemId,
            food.Stack,
            Game1.player.CurrentToolIndex,
            Game1.player.health,
            Game1.player.Stamina,
            requested);
    }

    private void TickConsumeFood()
    {
        if (activeConsumeFood is null)
        {
            return;
        }

        var active = activeConsumeFood;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteConsumeFoodBlocked(active, "consume_food_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteConsumeFoodBlocked(active, "consume_food_native_lifecycle_timeout");
            return;
        }

        switch (active.Stage)
        {
            case ConsumeFoodStage.PressUse:
                if (Game1.activeClickableMenu is not null ||
                    Game1.dialogueUp)
                {
                    CompleteConsumeFoodBlocked(
                        active,
                        "consume_food_pre_input_menu_state_drift");
                    return;
                }
                if (Game1.player.UsingTool ||
                    Game1.player.isEating ||
                    !Game1.player.CanMove ||
                    Game1.player.FarmerSprite.PauseForSingleAnimation)
                {
                    active.PreInputSettleTicks++;
                    if (active.PreInputSettleTicks > 180)
                    {
                        CompleteConsumeFoodBlocked(
                            active,
                            "consume_food_pre_input_animation_timeout");
                    }
                    return;
                }
                active.PreInputSettleTicks = 0;
                if (!ConsumeFoodSlotMatches(active))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_slot_drift_before_input");
                    return;
                }

                Game1.player.CurrentToolIndex = active.FoodSlotIndex;
                if (!TryApplySmapiRightButtonOverride(pressed: true, out var pressReason))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_right_press_failed:" + pressReason);
                    return;
                }
                active.RightButtonHeld = true;
                active.Stage = ConsumeFoodStage.ReleaseUse;
                return;

            case ConsumeFoodStage.ReleaseUse:
                ReleaseConsumeFoodRightButton(active);
                active.Stage = ConsumeFoodStage.WaitForPrompt;
                return;

            case ConsumeFoodStage.WaitForPrompt:
                if (Game1.activeClickableMenu is DialogueBox && string.Equals(Game1.currentLocation.lastQuestionKey, "Eat", StringComparison.Ordinal))
                {
                    active.Stage = ConsumeFoodStage.ConfirmPrompt;
                    return;
                }
                if (active.ElapsedTicks > 120)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_eat_prompt_not_observed");
                }
                return;

            case ConsumeFoodStage.ConfirmPrompt:
                if (Game1.activeClickableMenu is not DialogueBox prompt ||
                    !string.Equals(Game1.currentLocation.lastQuestionKey, "Eat", StringComparison.Ordinal))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_eat_prompt_drift");
                    return;
                }
                if (prompt.transitioning || prompt.safetyTimer > 0)
                {
                    return;
                }

                if (!TryApplySmapiButtonOverride(SButton.Y, pressed: true, out var confirmReason))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_confirm_press_failed:" + confirmReason);
                    return;
                }
                active.ConfirmationButtonHeld = true;
                active.NativeConfirmationIssued = true;
                active.Stage = ConsumeFoodStage.ReleaseConfirmation;
                return;

            case ConsumeFoodStage.ReleaseConfirmation:
                ReleaseConsumeFoodConfirmationButton(active);
                active.Stage = ConsumeFoodStage.WaitForPromptClose;
                return;

            case ConsumeFoodStage.WaitForPromptClose:
                if (Game1.activeClickableMenu is DialogueBox &&
                    string.Equals(Game1.currentLocation.lastQuestionKey, "Eat", StringComparison.Ordinal))
                {
                    return;
                }
                if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_unexpected_menu_after_confirmation");
                    return;
                }
                active.Stage = ConsumeFoodStage.WaitForCompletion;
                return;

            case ConsumeFoodStage.WaitForCompletion:
                active.EatingObserved |= Game1.player.isEating;
                if (Game1.player.isEating || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
                {
                    return;
                }
                if (!active.EatingObserved)
                {
                    return;
                }

                var stackAfter = ConsumeFoodStackAt(active.FoodSlotIndex, active.FoodQualifiedItemId);
                if (stackAfter != active.FoodStackBefore - 1)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_stack_delta_mismatch");
                    return;
                }
                if (Game1.player.health <= active.HealthBefore)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_health_not_recovered");
                    return;
                }

                CompleteConsumeFood(active, stackAfter);
                return;
        }
    }

    private static bool ConsumeFoodSlotMatches(ActiveConsumeFood active)
    {
        return active.FoodSlotIndex >= 0 && active.FoodSlotIndex < Game1.player.Items.Count &&
            Game1.player.Items[active.FoodSlotIndex] is StardewValley.Object food &&
            string.Equals(food.QualifiedItemId, active.FoodQualifiedItemId, StringComparison.Ordinal) &&
            food.Stack == active.FoodStackBefore;
    }

    private static int ConsumeFoodStackAt(int slotIndex, string qualifiedItemId)
    {
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count || Game1.player.Items[slotIndex] is not Item item)
        {
            return 0;
        }
        return string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal) ? item.Stack : 0;
    }

    private void CompleteConsumeFood(ActiveConsumeFood active, int stackAfter)
    {
        ReleaseConsumeFoodRightButton(active);
        ReleaseConsumeFoodConfirmationButton(active);
        RestoreConsumeFoodSlot(active);
        activeConsumeFood = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            EnergyBefore = active.EnergyBefore,
            EnergyAfter = Game1.player.Stamina,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "consume_food",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_right_click_opened_eat_prompt", "native_eat_yes_completed", "exact_food_stack_decremented", "health_recovery_observed", "previous_toolbar_slot_restored" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = ConsumeFoodObservedEffect(active.FoodSlotIndex),
            RecoveryFoodSlotIndex = active.FoodSlotIndex,
            RecoveryFoodQualifiedItemId = active.FoodQualifiedItemId,
            RecoveryFoodStackBefore = active.FoodStackBefore,
            RecoveryFoodStackAfter = stackAfter,
            RecoveryHealthBefore = active.HealthBefore,
            RecoveryHealthAfter = Game1.player.health,
            RecoveryRestoreSlotIndex = active.RestoreSlotIndex,
            RecoverySafetyStatus = "native_eating_lifecycle_verified",
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory[" + active.FoodSlotIndex + "].stack", Before = active.FoodStackBefore.ToString(), After = stackAfter.ToString() },
                new SimulatedFactChange { Path = "player.health", Before = active.HealthBefore.ToString(), After = Game1.player.health.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = active.EnergyBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteConsumeFoodBlocked(ActiveConsumeFood active, string reason)
    {
        ReleaseConsumeFoodRightButton(active);
        ReleaseConsumeFoodConfirmationButton(active);
        RestoreConsumeFoodSlot(active);
        activeConsumeFood = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "consume_food", active.RequestedEffect, ConsumeFoodObservedEffect(active.FoodSlotIndex), reason);
        result.RecoveryFoodSlotIndex = active.FoodSlotIndex;
        result.RecoveryFoodQualifiedItemId = active.FoodQualifiedItemId;
        result.RecoveryFoodStackBefore = active.FoodStackBefore;
        result.RecoveryFoodStackAfter = ConsumeFoodStackAt(active.FoodSlotIndex, active.FoodQualifiedItemId);
        result.RecoveryHealthBefore = active.HealthBefore;
        result.RecoveryHealthAfter = Game1.player.health;
        result.RecoveryRestoreSlotIndex = active.RestoreSlotIndex;
        result.RecoverySafetyStatus = "blocked_or_drifted";
        active.Pending.Completion.SetResult(result);
    }

    private void ReleaseConsumeFoodRightButton(ActiveConsumeFood active)
    {
        if (!active.RightButtonHeld)
        {
            return;
        }
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        active.RightButtonHeld = false;
    }

    private void ReleaseConsumeFoodConfirmationButton(ActiveConsumeFood active)
    {
        if (!active.ConfirmationButtonHeld)
        {
            return;
        }
        TryApplySmapiButtonOverride(SButton.Y, pressed: false, out _);
        active.ConfirmationButtonHeld = false;
    }

    private static void RestoreConsumeFoodSlot(ActiveConsumeFood active)
    {
        if (active.RestoreSlotIndex >= 0 && active.RestoreSlotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
    }

    private static string ConsumeFoodObservedEffect(int? slotIndex)
    {
        var slot = "missing";
        if (slotIndex.HasValue && slotIndex.Value >= 0 && slotIndex.Value < Game1.player.Items.Count && Game1.player.Items[slotIndex.Value] is Item item)
        {
            slot = item.QualifiedItemId + ":stack=" + item.Stack;
        }
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";slot=" + (slotIndex?.ToString() ?? "missing") + ":" + slot +
            ";health=" + Game1.player.health + ";energy=" + Game1.player.Stamina.ToString("0.###") +
            ";is_eating=" + Game1.player.isEating.ToString().ToLowerInvariant() +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }
}
