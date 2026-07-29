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
    private bool TryStartEmergencyCombatFood(MineShaft mine)
    {
        var nearbyDamage = mine.characters
            .OfType<Monster>()
            .Where(monster =>
                monster.Health > 0 &&
                ManhattanDistance(
                    Game1.player.TilePoint,
                    monster.TilePoint) <= 4)
            .Select(monster => Math.Max(0, monster.DamageToFarmer))
            .DefaultIfEmpty(0)
            .Max();
        var recoveryThreshold = Math.Max(
            Game1.player.maxHealth * 3 / 4,
            nearbyDamage * 3 + 1);
        if (Game1.player.health > recoveryThreshold ||
            Game1.player.health >= Game1.player.maxHealth ||
            Game1.activeClickableMenu is not null ||
            Game1.dialogueUp ||
            Game1.player.UsingTool ||
            !Game1.player.CanMove ||
            Game1.player.FarmerSprite.PauseForSingleAnimation ||
            Game1.player.hasBuff("25") ||
            Game1.player.hasBuff("6") ||
            Game1.player.team.SpecialOrderRuleActive("SC_NO_FOOD") &&
            mine.getMineArea() == MineShaft.desertArea)
        {
            return false;
        }

        var food = Game1.player.Items
            .Select((item, index) => new
            {
                Item = item as StardewValley.Object,
                Index = index
            })
            .Where(row =>
                row.Item is not null &&
                row.Item.Edibility > 0 &&
                row.Item.healthRecoveredOnConsumption() > 0)
            .OrderByDescending(row =>
                row.Item!.healthRecoveredOnConsumption())
            .ThenBy(row => row.Index)
            .FirstOrDefault();
        if (food?.Item is null)
        {
            return false;
        }

        ReleaseManualAutoCombatInput();
        RestoreManualAutoCombatTool();
        StopAllMovement();
        activeEmergencyCombatFood = new ActiveEmergencyCombatFood(
            mine,
            food.Index,
            food.Item.QualifiedItemId,
            food.Item.Stack,
            Game1.player.CurrentToolIndex,
            Game1.player.health);
        Monitor.Log(
            "Emergency combat recovery started with " +
                food.Item.QualifiedItemId +
                " at health " +
                Game1.player.health +
                ".",
            LogLevel.Info);
        return true;
    }

    private void TickEmergencyCombatFood()
    {
        var active = activeEmergencyCombatFood;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Mine) ||
            active.ElapsedTicks > 900)
        {
            FinishEmergencyCombatFood(
                active,
                "location_or_timeout");
            return;
        }

        ReleaseManualAutoCombatInput();
        StopAllMovement();
        switch (active.Stage)
        {
            case ConsumeFoodStage.PressUse:
                if (Game1.activeClickableMenu is not null ||
                    Game1.dialogueUp)
                {
                    FinishEmergencyCombatFood(
                        active,
                        "unexpected_menu_before_input");
                    return;
                }
                if (Game1.player.UsingTool ||
                    Game1.player.isEating ||
                    !Game1.player.CanMove ||
                    Game1.player.FarmerSprite.PauseForSingleAnimation)
                {
                    active.SettleTicks++;
                    if (active.SettleTicks > 180)
                    {
                        FinishEmergencyCombatFood(
                            active,
                            "pre_input_animation_timeout");
                    }
                    return;
                }
                if (!EmergencyCombatFoodSlotMatches(active))
                {
                    FinishEmergencyCombatFood(
                        active,
                        "food_slot_drift");
                    return;
                }
                Game1.player.CurrentToolIndex = active.SlotIndex;
                if (!TryApplySmapiRightButtonOverride(
                        pressed: true,
                        out var pressReason))
                {
                    FinishEmergencyCombatFood(
                        active,
                        "right_press_failed:" + pressReason);
                    return;
                }
                active.RightButtonHeld = true;
                active.Stage = ConsumeFoodStage.ReleaseUse;
                return;

            case ConsumeFoodStage.ReleaseUse:
                ReleaseEmergencyCombatFoodRightButton(active);
                active.Stage = ConsumeFoodStage.WaitForPrompt;
                return;

            case ConsumeFoodStage.WaitForPrompt:
                if (Game1.activeClickableMenu is DialogueBox &&
                    string.Equals(
                        Game1.currentLocation.lastQuestionKey,
                        "Eat",
                        StringComparison.Ordinal))
                {
                    active.Stage = ConsumeFoodStage.ConfirmPrompt;
                    return;
                }
                if (active.ElapsedTicks > 240)
                {
                    FinishEmergencyCombatFood(
                        active,
                        "eat_prompt_not_observed");
                }
                return;

            case ConsumeFoodStage.ConfirmPrompt:
                if (Game1.activeClickableMenu is not DialogueBox ||
                    !string.Equals(
                        Game1.currentLocation.lastQuestionKey,
                        "Eat",
                        StringComparison.Ordinal))
                {
                    FinishEmergencyCombatFood(
                        active,
                        "eat_prompt_drift");
                    return;
                }
                Game1.currentLocation.answerDialogueAction(
                    "Eat_Yes",
                    new[] { "Eat" });
                Game1.activeClickableMenu = null;
                Game1.dialogueUp = false;
                active.Stage = ConsumeFoodStage.WaitForCompletion;
                return;

            case ConsumeFoodStage.WaitForCompletion:
                active.EatingObserved |= Game1.player.isEating;
                if (Game1.player.isEating ||
                    !Game1.player.CanMove ||
                    Game1.player.FarmerSprite.PauseForSingleAnimation)
                {
                    return;
                }
                if (!active.EatingObserved)
                {
                    return;
                }

                var stackAfter = ConsumeFoodStackAt(
                    active.SlotIndex,
                    active.QualifiedItemId);
                var status = stackAfter == active.StackBefore - 1
                    ? "native_food_consumed"
                    : "food_stack_delta_mismatch";
                FinishEmergencyCombatFood(active, status);
                return;
        }
    }

    private static bool EmergencyCombatFoodSlotMatches(
        ActiveEmergencyCombatFood active)
    {
        return active.SlotIndex >= 0 &&
            active.SlotIndex < Game1.player.Items.Count &&
            Game1.player.Items[active.SlotIndex] is
                StardewValley.Object food &&
            string.Equals(
                food.QualifiedItemId,
                active.QualifiedItemId,
                StringComparison.Ordinal) &&
            food.Stack == active.StackBefore;
    }

    private void FinishEmergencyCombatFood(
        ActiveEmergencyCombatFood active,
        string status)
    {
        ReleaseEmergencyCombatFoodRightButton(active);
        if (!Game1.player.UsingTool &&
            active.RestoreSlotIndex >= 0 &&
            active.RestoreSlotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        Monitor.Log(
            "Emergency combat recovery " +
                status +
                ": " +
                active.QualifiedItemId +
                ", health " +
                active.HealthBefore +
                "->" +
                Game1.player.health +
                ".",
            status == "native_food_consumed"
                ? LogLevel.Info
                : LogLevel.Warn);
        activeEmergencyCombatFood = null;
    }

    private void ReleaseEmergencyCombatFoodRightButton(
        ActiveEmergencyCombatFood active)
    {
        if (!active.RightButtonHeld)
        {
            return;
        }
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        active.RightButtonHeld = false;
    }
}
