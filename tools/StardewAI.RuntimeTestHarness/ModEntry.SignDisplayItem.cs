using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;
using StardewValley.SaveSerialization;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetSignDisplayItem(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "GameLocation.checkAction->Sign.checkForAction(CurrentItem.getOne,no_inventory_consumption)->displayItem/displayType";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].sign_state.display_item=" + request.QualifiedItemId + ";display_type=" + request.SignExpectedDisplayType +
            ";player.inventory[" + request.InventorySlotIndex + "].stack_and_state=unchanged";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.SignDisplaySourceQuality.HasValue ||
            !request.SignExpectedDisplayType.HasValue || !request.SignPreviousDisplayType.HasValue ||
            !request.SignReplaceExistingDisplay.HasValue || !request.SignAllowReplaceExistingDisplay.HasValue)
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "typed_projection=missing", "set_sign_display_item_typed_projection_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(Sign).FullName, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "native_or_target_contract_mismatch", "set_sign_display_item_contract_mismatch");
        }
        if (request.SignReplaceExistingDisplay != request.SignAllowReplaceExistingDisplay)
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "replacement_authorization_mismatch", "set_sign_display_item_replacement_not_authorized");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "set_sign_display_item_location_mismatch");
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.objects.TryGetValue(target.ToVector2(), out var targetObject) ||
            targetObject is not Sign sign || sign.GetType() != typeof(Sign) || sign.Location != location || sign.TileLocation != target.ToVector2())
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "target_runtime_type=" + (targetObject?.GetType().FullName ?? "missing"), "set_sign_display_item_exact_base_sign_required");
        }
        var targetStateBefore = DirectRuntimeItemState.From(sign)!;
        if (!string.Equals(sign.QualifiedItemId, request.SignDisplayTargetQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(targetStateBefore.StateSha256, request.SignDisplayTargetStateSha256, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "target_qid_or_state_hash_mismatch", "set_sign_display_item_target_state_drifted");
        }
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y, "set_sign_display_item_player_not_adjacent");
        }

        var previousState = DirectRuntimeItemState.From(sign.displayItem.Value);
        if ((sign.displayItem.Value is not null) != request.SignReplaceExistingDisplay.Value ||
            sign.displayType.Value != request.SignPreviousDisplayType.Value ||
            !string.Equals(sign.displayItem.Value?.QualifiedItemId ?? string.Empty, request.SignPreviousDisplayItemQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(sign.displayItem.Value?.GetType().FullName ?? string.Empty, request.SignPreviousDisplayItemRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(previousState?.StateSha256 ?? string.Empty, request.SignPreviousDisplayItemStateSha256, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                SignDisplayObservedEffect(sign), "set_sign_display_item_previous_payload_drifted");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count || Game1.player.Items[slot] is not Item source ||
            source.Stack != request.ExpectedStackBefore.Value || source.Quality != request.SignDisplaySourceQuality.Value ||
            !string.Equals(source.ItemId, request.ItemId, StringComparison.Ordinal) ||
            !string.Equals(source.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(source.GetType().FullName, request.SignDisplaySourceRuntimeType, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "source_inventory_identity_mismatch", "set_sign_display_item_source_identity_drifted");
        }
        var sourceStateBefore = DirectRuntimeItemState.From(source)!;
        if (!string.Equals(sourceStateBefore.StateSha256, request.SignDisplaySourceStateSha256, StringComparison.Ordinal) ||
            RuntimeSignDisplayType(source) != request.SignExpectedDisplayType.Value)
        {
            return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                "source_state_or_display_type_mismatch", "set_sign_display_item_source_projection_drifted");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var previousSlot = Game1.player.CurrentToolIndex;
        bool handled;
        try
        {
            Game1.player.CurrentToolIndex = slot;
            if (!ReferenceEquals(Game1.player.CurrentItem, source))
            {
                return BlockedWithPrimitive(request, "set_sign_display_item", requested,
                    "current_item_identity_mismatch", "set_sign_display_item_active_slot_drifted");
            }
            handled = location.checkAction(
                new TileLocation(target.X, target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
        }
        finally
        {
            Game1.player.CurrentToolIndex = previousSlot;
        }

        var sourceAfter = Game1.player.Items.ElementAtOrDefault(slot);
        var sourceStateAfter = DirectRuntimeItemState.From(sourceAfter);
        var display = sign.displayItem.Value;
        var displayState = DirectRuntimeItemState.From(display);
        var sourcePreserved = ReferenceEquals(sourceAfter, source) && sourceStateAfter is not null &&
            sourceAfter!.Stack == request.ExpectedStackBefore.Value &&
            string.Equals(sourceStateAfter.StateSha256, sourceStateBefore.StateSha256, StringComparison.Ordinal);
        var verified = handled && sourcePreserved && display is not null && displayState is not null &&
            !ReferenceEquals(display, source) && display.Stack == 1 &&
            string.Equals(display.QualifiedItemId, source.QualifiedItemId, StringComparison.Ordinal) &&
            display.Quality == source.Quality && sign.displayType.Value == request.SignExpectedDisplayType.Value;

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
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "set_sign_display_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_GameLocation_checkAction_dispatched_exact_base_Sign",
                    "native_getOne_created_distinct_display_item",
                    "display_identity_quality_stack_and_type_verified",
                    "source_inventory_reference_stack_and_serialized_state_unchanged"
                }
                : new[] { "set_sign_display_item_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = SignDisplayObservedEffect(sign) +
                ";display_state_sha256=" + (displayState?.StateSha256 ?? "null") +
                ";source_state_before=" + sourceStateBefore.StateSha256 +
                ";source_state_after=" + (sourceStateAfter?.StateSha256 ?? "null") +
                ";source_reference_preserved=" + ReferenceEquals(sourceAfter, source).ToString().ToLowerInvariant() +
                ";handled=" + handled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "set_sign_display_item_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].sign_state.display_item",
                        Before = request.SignPreviousDisplayItemQualifiedItemId + ":" + request.SignPreviousDisplayItemStateSha256,
                        After = display!.QualifiedItemId + ":" + displayState!.StateSha256
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "].state_sha256",
                        Before = sourceStateBefore.StateSha256,
                        After = sourceStateAfter!.StateSha256
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static int RuntimeSignDisplayType(Item item) => item switch
    {
        Hat => 2,
        Ring => 4,
        Furniture => 5,
        StardewValley.Object obj when obj.bigCraftable.Value => 3,
        _ => 1
    };

    private static string SignDisplayObservedEffect(Sign sign) =>
        "target_runtime_type=" + sign.GetType().FullName +
        ";display_item=" + (sign.displayItem.Value?.QualifiedItemId ?? "null") +
        ";display_runtime_type=" + (sign.displayItem.Value?.GetType().FullName ?? "null") +
        ";display_quality=" + (sign.displayItem.Value?.Quality.ToString() ?? "null") +
        ";display_stack=" + (sign.displayItem.Value?.Stack.ToString() ?? "null") +
        ";display_type=" + sign.displayType.Value;

    private sealed record DirectRuntimeItemState(string StateSha256, int StateBytes)
    {
        public static DirectRuntimeItemState? From(Item? item)
        {
            if (item is null)
            {
                return null;
            }
            using var stream = new MemoryStream();
            SaveSerializer.GetSerializer(item.GetType()).Serialize(stream, item);
            var bytes = stream.ToArray();
            return new DirectRuntimeItemState(
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.Length);
        }
    }
}
