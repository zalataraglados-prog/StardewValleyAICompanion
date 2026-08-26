using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteEditTextSign(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "GameLocation.checkAction->Object.CheckForActionOnTextSign->TitleTextInputMenu(textLimit=60,minLength=0,paste=false)->NamingMenu.textBoxEnter(FilterDirtyWords)->signText=text.Trim()->TokenParser.ParseText+FilterDirtyWords->showNextIndex=IsNullOrEmpty(SignText)";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].sign_state.sign_text=native_menu_receipt";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.TextSignShowNextIndexBefore.HasValue || !request.TextSignReplacesExistingText.HasValue ||
            !request.TextSignAllowReplaceExistingText.HasValue || request.TextSignRequestedText is null)
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "typed_projection=missing", "edit_text_sign_typed_projection_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "native_or_target_contract_mismatch", "edit_text_sign_contract_mismatch");
        }
        if (request.TextSignRequestedText.Length > 60 ||
            request.TextSignRequestedText.Any(character => character == '"' || char.IsControl(character)))
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "requested_text_not_keyboard_representable", "edit_text_sign_native_keyboard_input_invalid");
        }
        if (request.TextSignReplacesExistingText != request.TextSignAllowReplaceExistingText)
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "replacement_authorization_mismatch", "edit_text_sign_replacement_not_authorized");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "edit_text_sign_location_mismatch");
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.objects.TryGetValue(target.ToVector2(), out var targetObject) ||
            targetObject.GetType() != typeof(StardewObject) || !targetObject.IsTextSign() ||
            targetObject.Location != location || targetObject.TileLocation != target.ToVector2())
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "target_runtime_type=" + (targetObject?.GetType().FullName ?? "missing"), "edit_text_sign_exact_base_object_required");
        }
        var sign = targetObject;
        var targetStateBefore = DirectRuntimeItemState.From(sign)!;
        if (!string.Equals(sign.QualifiedItemId, request.TextSignTargetQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(targetStateBefore.StateSha256, request.TextSignTargetStateSha256, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "target_qid_or_state_hash_mismatch", "edit_text_sign_target_state_drifted");
        }
        if (!string.Equals(sign.signText.Value ?? string.Empty, request.TextSignRawBefore, StringComparison.Ordinal) ||
            !string.Equals(sign.SignText ?? string.Empty, request.TextSignDisplayBefore, StringComparison.Ordinal) ||
            sign.showNextIndex.Value != request.TextSignShowNextIndexBefore.Value ||
            !request.TextSignReplacesExistingText.Value.Equals(!string.IsNullOrEmpty(sign.signText.Value)))
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                TextSignObservedEffect(sign), "edit_text_sign_previous_text_drifted");
        }
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y, "edit_text_sign_player_not_adjacent");
        }
        if (Game1.activeClickableMenu is not null)
        {
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "active_menu=" + Game1.activeClickableMenu.GetType().FullName, "edit_text_sign_menu_must_be_clear");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = location.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled || Game1.activeClickableMenu is not TitleTextInputMenu menu ||
            menu.GetType() != typeof(TitleTextInputMenu) || menu.pasteButton.visible ||
            menu.textBox.textLimit != 60 || menu.minLength != 0 || !menu.FilterInput)
        {
            Game1.exitActiveMenu();
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "handled=" + handled.ToString().ToLowerInvariant() + ";menu=" + (Game1.activeClickableMenu?.GetType().FullName ?? "none"),
                "edit_text_sign_native_menu_contract_mismatch");
        }

        while (menu.textBox.Text.Length > 0)
        {
            menu.textBox.RecieveCommandInput('\b');
        }
        foreach (var character in request.TextSignRequestedText)
        {
            menu.textBox.RecieveTextInput(character);
        }
        if (!string.Equals(menu.textBox.Text, request.TextSignRequestedText, StringComparison.Ordinal))
        {
            Game1.exitActiveMenu();
            return BlockedWithPrimitive(request, "edit_text_sign", requested,
                "textbox_length=" + menu.textBox.Text.Length, "edit_text_sign_textbox_input_mismatch");
        }
        var done = menu.doneNamingButton.bounds.Center;
        menu.receiveLeftClick(done.X, done.Y, playSound: false);

        var expectedRaw = Utility.FilterDirtyWords(request.TextSignRequestedText).Trim();
        var expectedDisplay = Utility.FilterDirtyWords(TokenParser.ParseText(expectedRaw));
        var targetStateAfter = DirectRuntimeItemState.From(sign)!;
        var verified = Game1.activeClickableMenu is null &&
            string.Equals(sign.signText.Value ?? string.Empty, expectedRaw, StringComparison.Ordinal) &&
            string.Equals(sign.SignText ?? string.Empty, expectedDisplay, StringComparison.Ordinal) &&
            sign.showNextIndex.Value == string.IsNullOrEmpty(sign.SignText);

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
            PrimitiveKind = "edit_text_sign",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_GameLocation_checkAction_opened_exact_TitleTextInputMenu",
                    "native_keyboard_input_obeyed_60_code_unit_limit",
                    "native_filter_trim_token_display_pipeline_verified",
                    "showNextIndex_empty_display_rule_verified"
                }
                : new[] { "edit_text_sign_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = TextSignObservedEffect(sign) +
                ";expected_raw_text=" + expectedRaw + ";expected_display_text=" + expectedDisplay +
                ";target_state_before=" + targetStateBefore.StateSha256 +
                ";target_state_after=" + targetStateAfter.StateSha256,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "edit_text_sign_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].sign_state.raw_sign_text",
                        Before = request.TextSignRawBefore,
                        After = sign.signText.Value ?? string.Empty
                    },
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].sign_state.sign_text",
                        Before = request.TextSignDisplayBefore,
                        After = sign.SignText ?? string.Empty
                    },
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].sign_state.show_next_index",
                        Before = request.TextSignShowNextIndexBefore.Value.ToString().ToLowerInvariant(),
                        After = sign.showNextIndex.Value.ToString().ToLowerInvariant()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string TextSignObservedEffect(StardewObject sign) =>
        "target_runtime_type=" + sign.GetType().FullName +
        ";raw_sign_text=" + (sign.signText.Value ?? string.Empty) +
        ";display_sign_text=" + (sign.SignText ?? string.Empty) +
        ";show_next_index=" + sign.showNextIndex.Value.ToString().ToLowerInvariant();
}
