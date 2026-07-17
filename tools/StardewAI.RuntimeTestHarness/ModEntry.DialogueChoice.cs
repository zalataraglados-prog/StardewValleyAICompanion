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
    private TrainingExecutionResult ExecuteChooseDialogueResponse(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), reasons.ToArray());
        }

        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_box_not_open");
        }

        var expectedKey = request.ExpectedDialogueKey;
        var actualKey = Game1.currentLocation.lastQuestionKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(actualKey, expectedKey, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_key_mismatch");
        }

        var responseKey = request.DialogueResponseKey;
        if (!IsDialogueResponseWhitelisted(expectedKey, responseKey, request.ExpectedShopId))
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_response_not_whitelisted");
        }

        var response = menu.responses?.FirstOrDefault(item => string.Equals(item.responseKey, responseKey, StringComparison.Ordinal));
        if (response is null)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_response_key_not_available");
        }

        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeQuestionKey = actualKey;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = Game1.currentLocation.answerDialogue(response);
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterShopId = Game1.activeClickableMenu is ShopMenu shopMenu ? shopMenu.ShopId : string.Empty;
        var verified = handled &&
            string.Equals(afterMenuType, "ShopMenu", StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(request.ExpectedShopId) || string.Equals(afterShopId, request.ExpectedShopId, StringComparison.OrdinalIgnoreCase));
        var verificationReasons = verified
            ? new[] { "dialogue_response_handled", "expected_shop_menu_opened" }
            : new[] { handled ? "dialogue_response_handled_without_expected_shop_menu" : "dialogue_response_not_handled" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "choose_dialogue_response",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = DialogueRequestedEffect(request),
            ObservedEffect = DialogueObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType },
                new SimulatedFactChange { Path = "menus.active_menu.shop_id", Before = "", After = afterShopId },
                new SimulatedFactChange { Path = "menus.active_menu.last_question_key", Before = beforeQuestionKey, After = Game1.currentLocation.lastQuestionKey ?? string.Empty }
            }
        };
    }

    private static bool IsDialogueResponseWhitelisted(string expectedDialogueKey, string responseKey, string expectedShopId)
    {
        return DialogueResponseOpensExpectedShop(expectedDialogueKey, responseKey, expectedShopId);
    }

    private static bool DialogueResponseOpensExpectedShop(string expectedDialogueKey, string responseKey, string expectedShopId)
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

    private static string DialogueRequestedEffect(TrainingExecutionRequest request)
    {
        return "dialogue_key=" + (string.IsNullOrWhiteSpace(request.ExpectedDialogueKey) ? "missing" : request.ExpectedDialogueKey) +
            ";response_key=" + (string.IsNullOrWhiteSpace(request.DialogueResponseKey) ? "missing" : request.DialogueResponseKey) +
            ";expected_shop_id=" + (string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "missing" : request.ExpectedShopId);
    }

    private static string DialogueObservedEffect()
    {
        return "menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";last_question_key=" + (Game1.currentLocation?.lastQuestionKey ?? "none") +
            ";shop_id=" + (Game1.activeClickableMenu is ShopMenu menu ? menu.ShopId : "none");
    }
}
