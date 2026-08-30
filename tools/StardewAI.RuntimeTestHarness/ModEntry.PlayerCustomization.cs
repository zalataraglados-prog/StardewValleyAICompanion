using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.MakeoverOutfits;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string PlayerCustomizationNativeContract =
        "wizard_shrine:shared_route->WizardShrine_checkAction->answerDialogue(Yes)->CharacterCustomization(Source.Wizard)_native_controls->OK;desert_makeover:shared_route->walk_onto_DesertMakeover_TouchAction->native_skippable_Event->onEventFinished_ReceiveMakeOver";

    private void StartPlayerCustomization(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0) { pending.Completion.SetResult(Blocked(request, reasons.ToArray())); return; }
        if (!PlayerCustomizationRequestIsTyped(request))
        {
            pending.Completion.SetResult(PlayerCustomizationBlocked(request, "player_customization_complete_typed_request_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(PlayerCustomizationBlocked(request, "player_customization_player_menu_or_event_not_ready"));
            return;
        }
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var liveReasons = ValidatePlayerCustomizationLiveState(location, target, stand, request);
        if (liveReasons.Length > 0)
        {
            pending.Completion.SetResult(PlayerCustomizationBlocked(request, liveReasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(PlayerCustomizationBlocked(request, "player_customization_path_unavailable:" + pathReason));
            return;
        }
        activePlayerCustomization = new ActivePlayerCustomization(pending, location, target, stand, path, maxMovementTiles);
    }

    private static bool PlayerCustomizationRequestIsTyped(TrainingExecutionRequest request)
    {
        if (request.CustomizationMode is not ("wizard_shrine" or "desert_makeover") ||
            string.IsNullOrWhiteSpace(request.CustomizationReason) || request.ConfirmCustomization != true ||
            request.CustomizationProjectionFingerprint.Length != 64 || !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            request.NativeContract != PlayerCustomizationNativeContract)
            return false;
        if (request.CustomizationMode == "desert_makeover")
            return !string.IsNullOrWhiteSpace(request.CustomizationStylistName) &&
                request.CustomizationPassiveFestivalDay is > 0 && request.CustomizationFreeInventorySlots.HasValue &&
                request.CustomizationEquippedItemCount.HasValue && request.CustomizationExpectedOutfitIndex is >= 0;
        return !string.IsNullOrWhiteSpace(request.CustomizationName) &&
            !string.IsNullOrWhiteSpace(request.CustomizationFavoriteThing) && request.CustomizationGender is "male" or "female" &&
            request.CustomizationSkinIndex is >= 0 and <= 23 && request.CustomizationAccessoryIndex is >= -1 and <= 29 &&
            request.CustomizationHairStyleId.HasValue && PlayerCustomizationSliders(request).All(value => value is >= 0 and <= 100) &&
            request.CustomizationPriceGold == 500 && request.CustomizationMoneyBefore is >= 500;
    }

    private static string[] ValidatePlayerCustomizationLiveState(
        GameLocation location, Point target, Point stand, TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            request.CustomizationActionToken != (request.CustomizationMode == "wizard_shrine" ? "WizardShrine" : "DesertMakeover"))
            reasons.Add("player_customization_location_or_action_token_drifted");
        if (request.CustomizationMode == "wizard_shrine")
        {
            if (location.NameOrUniqueName != "WizardHouseBasement" ||
                location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") != "WizardShrine" ||
                request.CustomizationActionRaw != "WizardShrine" || !AreAdjacent(target, stand) ||
                Game1.player.Money != request.CustomizationMoneyBefore || Game1.player.Money < 500 ||
                !Farmer.GetAllHairstyleIndices().Contains(request.CustomizationHairStyleId!.Value))
                reasons.Add("player_customization_wizard_endpoint_price_or_target_domain_drifted");
        }
        else if (location is not DesertFestival desert || target != stand ||
            location.doesTileHaveProperty(target.X, target.Y, "TouchAction", "Back")?.Split(' ')[0] != "DesertMakeover" ||
            desert.GetStylist()?.Name != request.CustomizationStylistName ||
            Utility.GetDayOfPassiveFestival("DesertFestival") != request.CustomizationPassiveFestivalDay ||
            Game1.player.activeDialogueEvents.ContainsKey("DesertMakeover") ||
            Game1.player.freeSpotsInInventory() != request.CustomizationFreeInventorySlots ||
            Game1.player.freeSpotsInInventory() < request.CustomizationEquippedItemCount ||
            !RuntimeDesertMakeoverProjectionMatches(request))
            reasons.Add("player_customization_desert_touch_stylist_inventory_daily_or_rng_projection_drifted");
        if (!IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("player_customization_interaction_geometry_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickPlayerCustomization()
    {
        var active = activePlayerCustomization;
        if (active is null) return;
        active.ElapsedTicks++; active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks) { CompletePlayerCustomization(active, false, PlayerCustomizationTimeoutReason(active)); return; }
        if (active.Pending.Request.CustomizationMode == "desert_makeover") TickDesertMakeover(active);
        else TickWizardCustomization(active);
    }

    private void TickWizardCustomization(ActivePlayerCustomization active)
    {
        var request = active.Pending.Request;
        if (!active.ActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "player_customization_wizard", out var failure);
            if (movement == NativeObjectMovementStatus.Failed) { CompletePlayerCustomization(active, false, failure); return; }
            if (movement == NativeObjectMovementStatus.Moving) return;
            var reasons = ValidatePlayerCustomizationLiveState(active.Location, active.Target, active.Stand, request);
            if (reasons.Length > 0) { CompletePlayerCustomization(active, false, reasons); return; }
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
            active.Location.checkAction(new xTile.Dimensions.Location(active.Target.X, active.Target.Y), Game1.viewport, Game1.player);
            active.ActionIssued = true; active.StageTicks = 0; return;
        }
        if (!active.DialogueAnswered)
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion || active.Location.lastQuestionKey != "WizardShrine")
            {
                if (active.StageTicks > 180) CompletePlayerCustomization(active, false, "player_customization_wizard_question_timeout");
                return;
            }
            var yes = dialogue.responses?.FirstOrDefault(response => response.responseKey == "Yes");
            if (yes is null || !active.Location.answerDialogue(yes))
            {
                CompletePlayerCustomization(active, false, "player_customization_wizard_native_yes_failed"); return;
            }
            active.DialogueAnswered = true; active.NativeControlInputs++; active.StageTicks = 0; return;
        }
        if (Game1.activeClickableMenu is not CharacterCustomization menu || menu.source != CharacterCustomization.Source.Wizard)
        {
            if (active.OkClicked && Game1.activeClickableMenu is null) CompletePlayerCustomization(active, WizardCustomizationReceiptMatches(request));
            else if (active.StageTicks > 180) CompletePlayerCustomization(active, false, "player_customization_wizard_menu_timeout");
            return;
        }
        if (ApplyWizardDiscreteControl(menu, request, active)) return;
        if (ApplyWizardColorControl(menu, request, active)) return;
        if (!active.TextEntered)
        {
            var nameBox = Helper.Reflection.GetField<TextBox>(menu, "nameBox").GetValue();
            var favoriteBox = Helper.Reflection.GetField<TextBox>(menu, "favThingBox").GetValue();
            if (!EnterNativeText(nameBox, request.CustomizationName) || !EnterNativeText(favoriteBox, request.CustomizationFavoriteThing))
            {
                CompletePlayerCustomization(active, false, "player_customization_wizard_native_text_input_rejected"); return;
            }
            active.TextEntered = true; active.NativeControlInputs += 2; active.StageTicks = 0; return;
        }
        if (!WizardCustomizationMenuMatches(menu, request))
        {
            if (active.StageTicks > 120) CompletePlayerCustomization(active, false, "player_customization_wizard_menu_state_mismatch");
            return;
        }
        if (!active.OkClicked)
        {
            var ok = menu.okButton.bounds.Center; menu.receiveLeftClick(ok.X, ok.Y); active.OkClicked = true;
            active.NativeControlInputs++; active.StageTicks = 0; return;
        }
        if (Game1.activeClickableMenu is null) CompletePlayerCustomization(active, WizardCustomizationReceiptMatches(request));
    }

    private void TickDesertMakeover(ActivePlayerCustomization active)
    {
        var request = active.Pending.Request;
        if (!Game1.player.activeDialogueEvents.ContainsKey("DesertMakeover") && !active.ActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "player_customization_desert", out var failure);
            if (movement == NativeObjectMovementStatus.Failed) { CompletePlayerCustomization(active, false, failure); return; }
            if (movement == NativeObjectMovementStatus.Moving) return;
            // The map TouchAction is invoked by native movement onto the target tile.
            active.ActionIssued = true; active.StageTicks = 0; return;
        }
        if (Game1.player.activeDialogueEvents.ContainsKey("DesertMakeover")) active.ActionIssued = true;
        var nativeEvent = active.Location.currentEvent;
        if (nativeEvent is not null && Game1.eventUp)
        {
            active.EventSeen = true;
            if (nativeEvent.skippable && !active.EventSkipClicked)
            {
                nativeEvent.receiveMouseClick(Game1.viewport.Width - 52, Game1.viewport.Height - 34);
                active.EventSkipClicked = true; active.NativeControlInputs++; active.StageTicks = 0;
            }
            return;
        }
        if (active.EventSeen && !Game1.eventUp && Game1.player.controller is null && !Game1.freezeControls)
            CompletePlayerCustomization(active, DesertMakeoverReceiptMatches(active, request));
        else if (active.StageTicks > 600)
            CompletePlayerCustomization(active, false, "player_customization_desert_event_start_or_finish_timeout");
    }

    private static bool ApplyWizardDiscreteControl(CharacterCustomization menu, TrainingExecutionRequest request, ActivePlayerCustomization active)
    {
        if ((request.CustomizationGender == "male") != Game1.player.IsMale)
            return ClickNamed(menu, menu.genderButtons, request.CustomizationGender == "male" ? "Male" : "Female", active);
        if (Game1.player.skin.Value != request.CustomizationSkinIndex)
            return ClickNamed(menu, menu.rightSelectionButtons, "Skin", active);
        if (Game1.player.hair.Value != request.CustomizationHairStyleId)
            return ClickNamed(menu, menu.rightSelectionButtons, "Hair", active);
        if (Game1.player.accessory.Value != request.CustomizationAccessoryIndex)
            return ClickNamed(menu, menu.rightSelectionButtons, "Acc", active);
        return false;
    }

    private static bool ClickNamed(CharacterCustomization menu, IEnumerable<ClickableComponent> controls, string name,
        ActivePlayerCustomization active)
    {
        var control = controls.FirstOrDefault(value => value.name == name);
        if (control is null) return false;
        var point = control.bounds.Center; menu.receiveLeftClick(point.X, point.Y); active.NativeControlInputs++; active.StageTicks = 0;
        return true;
    }

    private static bool ApplyWizardColorControl(CharacterCustomization menu, TrainingExecutionRequest request, ActivePlayerCustomization active)
    {
        var targets = new[]
        {
            (522, request.CustomizationEyeHue!.Value, menu.eyeColorPicker.hueBar.value),
            (523, request.CustomizationEyeSaturation!.Value, menu.eyeColorPicker.saturationBar.value),
            (524, request.CustomizationEyeValue!.Value, menu.eyeColorPicker.valueBar.value),
            (525, request.CustomizationHairHue!.Value, menu.hairColorPicker.hueBar.value),
            (526, request.CustomizationHairSaturation!.Value, menu.hairColorPicker.saturationBar.value),
            (527, request.CustomizationHairValue!.Value, menu.hairColorPicker.valueBar.value)
        };
        foreach (var (id, target, current) in targets)
        {
            if (current == target) continue;
            var component = menu.colorPickerCCs.FirstOrDefault(value => value.myID == id);
            if (component is null) return false;
            if (target == 100 && current == 99)
            {
                menu.populateClickableComponentList();
                menu.setCurrentlySnappedComponentTo(id);
                menu.gamePadButtonHeld(Buttons.DPadRight);
            }
            else
            {
                var clickTarget = target == 100 ? 99 : target;
                var x = Enumerable.Range(component.bounds.Left, component.bounds.Width)
                    .First(value => (int)((value - component.bounds.Left) / (float)component.bounds.Width * 100f) == clickTarget);
                menu.receiveLeftClick(x, component.bounds.Center.Y);
                menu.releaseLeftClick(x, component.bounds.Center.Y);
            }
            active.NativeControlInputs++; active.StageTicks = 0; return true;
        }
        return false;
    }

    private static bool EnterNativeText(TextBox box, string target)
    {
        box.SelectMe();
        while (box.Text.Length > 0) box.RecieveCommandInput('\b');
        foreach (var character in target) box.RecieveTextInput(character);
        box.Selected = false;
        return box.Text == target;
    }

    private static bool WizardCustomizationMenuMatches(CharacterCustomization menu, TrainingExecutionRequest request) =>
        Game1.player.Name == request.CustomizationName && Game1.player.favoriteThing.Value == request.CustomizationFavoriteThing &&
        ((request.CustomizationGender == "male") == Game1.player.IsMale) && Game1.player.skin.Value == request.CustomizationSkinIndex &&
        Game1.player.hair.Value == request.CustomizationHairStyleId && Game1.player.accessory.Value == request.CustomizationAccessoryIndex &&
        menu.eyeColorPicker.hueBar.value == request.CustomizationEyeHue &&
        menu.eyeColorPicker.saturationBar.value == request.CustomizationEyeSaturation &&
        menu.eyeColorPicker.valueBar.value == request.CustomizationEyeValue &&
        menu.hairColorPicker.hueBar.value == request.CustomizationHairHue &&
        menu.hairColorPicker.saturationBar.value == request.CustomizationHairSaturation &&
        menu.hairColorPicker.valueBar.value == request.CustomizationHairValue && menu.canLeaveMenu();

    private static bool WizardCustomizationReceiptMatches(TrainingExecutionRequest request) =>
        Game1.player.Money == request.CustomizationMoneyBefore - 500 && Game1.player.isCustomized.Value &&
        Game1.player.Name == request.CustomizationName && Game1.player.favoriteThing.Value == request.CustomizationFavoriteThing &&
        ((request.CustomizationGender == "male") == Game1.player.IsMale) && Game1.player.skin.Value == request.CustomizationSkinIndex &&
        Game1.player.hair.Value == request.CustomizationHairStyleId && Game1.player.accessory.Value == request.CustomizationAccessoryIndex &&
        Game1.player.newEyeColor.Value == ColorPicker.HsvToRgb(request.CustomizationEyeHue!.Value / 100d * 360d,
            request.CustomizationEyeSaturation!.Value / 100d, request.CustomizationEyeValue!.Value / 100d) &&
        Game1.player.hairstyleColor.Value == ColorPicker.HsvToRgb(request.CustomizationHairHue!.Value / 100d * 360d,
            request.CustomizationHairSaturation!.Value / 100d, request.CustomizationHairValue!.Value / 100d);

    private static bool DesertMakeoverReceiptMatches(ActivePlayerCustomization active, TrainingExecutionRequest request)
    {
        if (!Game1.player.activeDialogueEvents.ContainsKey("DesertMakeover") ||
            (Game1.player.hat.Value?.QualifiedItemId ?? string.Empty) != request.CustomizationExpectedHatQid ||
            (Game1.player.shirtItem.Value?.QualifiedItemId ?? string.Empty) != request.CustomizationExpectedShirtQid ||
            (Game1.player.pantsItem.Value?.QualifiedItemId ?? string.Empty) != request.CustomizationExpectedPantsQid ||
            !ClothingColorMatches(Game1.player.shirtItem.Value, request.CustomizationExpectedShirtColor) ||
            !ClothingColorMatches(Game1.player.pantsItem.Value, request.CustomizationExpectedPantsColor))
            return false;
        var returnedAdded = Game1.player.team.returnedDonations.Count - active.ReturnedBefore;
        var missingLoose = active.BeforeLooseCounts.Sum(pair => Math.Max(0,
            pair.Value + 1 - Game1.player.Items.Count(item => item?.QualifiedItemId == pair.Key)));
        return missingLoose <= returnedAdded;
    }

    private static bool ClothingColorMatches(Clothing? clothing, string expected) =>
        string.IsNullOrWhiteSpace(expected) || (Utility.StringToColor(expected) is { } color && clothing?.clothesColor.Value == color);

    private static bool RuntimeDesertMakeoverProjectionMatches(TrainingExecutionRequest request)
    {
        var source = DataLoader.MakeoverOutfits(Game1.content) ?? new List<MakeoverOutfit>();
        var qualifying = source.Select((outfit, index) => (outfit, index)).Where(row =>
            (!row.outfit.Gender.HasValue || row.outfit.Gender.Value == Game1.player.Gender) &&
            !(row.outfit.OutfitParts ?? new List<MakeoverItem>()).Where(part => part.MatchesGender(Game1.player.Gender))
                .Select(part => ItemRegistry.GetDataOrErrorItem(part.ItemId).QualifiedItemId)
                .Any(qid => qid == Game1.player.hat.Value?.QualifiedItemId || qid == Game1.player.shirtItem.Value?.QualifiedItemId)).ToList();
        if (qualifying.Count == 0) return false;
        var random = Utility.CreateDaySaveRandom(Game1.year);
        var usesPlayerSeed = random.NextDouble() < 0.75;
        if (usesPlayerSeed) random = Utility.CreateDaySaveRandom(Game1.year, (int)Game1.player.UniqueMultiplayerID);
        var selected = qualifying[random.Next(qualifying.Count)];
        var special = Utility.GetDayOfPassiveFestival("DesertFestival") == 2 && Utility.CreateDaySaveRandom().NextDouble() < 0.03;
        var expected = new Dictionary<string, (string Qid, string Color)>(StringComparer.Ordinal)
        {
            ["hat"] = (string.Empty, string.Empty), ["shirt"] = (string.Empty, string.Empty), ["pants"] = (string.Empty, string.Empty)
        };
        if (special)
        {
            expected["hat"] = ("(H)LaurelWreathCrown", string.Empty); expected["shirt"] = ("(S)1199", string.Empty);
            expected["pants"] = ("(P)3", "247 245 205");
        }
        else foreach (var part in selected.outfit.OutfitParts.Where(part => part.MatchesGender(Game1.player.Gender)))
        {
            var qid = ItemRegistry.GetDataOrErrorItem(part.ItemId).QualifiedItemId;
            var slot = qid.StartsWith("(H)") ? "hat" : qid.StartsWith("(S)") ? "shirt" : qid.StartsWith("(P)") ? "pants" : string.Empty;
            if (slot.Length > 0 && expected[slot].Qid.Length == 0) expected[slot] = (qid, part.Color ?? string.Empty);
        }
        return request.CustomizationExpectedOutfitIndex == selected.index && request.CustomizationUsesPlayerSeed == usesPlayerSeed &&
            request.CustomizationSpecialLaurelOutfit == special && request.CustomizationExpectedHatQid == expected["hat"].Qid &&
            request.CustomizationExpectedHatColor == expected["hat"].Color && request.CustomizationExpectedShirtQid == expected["shirt"].Qid &&
            request.CustomizationExpectedShirtColor == expected["shirt"].Color && request.CustomizationExpectedPantsQid == expected["pants"].Qid &&
            request.CustomizationExpectedPantsColor == expected["pants"].Color;
    }

    private void CompletePlayerCustomization(ActivePlayerCustomization active, bool verified, params string[] reasons)
    {
        StopAllMovement(); activePlayerCustomization = null;
        var request = active.Pending.Request;
        var verification = verified ? new[]
        {
            "shared_bfs_reached_exact_native_customization_endpoint",
            request.CustomizationMode == "wizard_shrine" ? "native_WizardShrine_Yes_and_CharacterCustomization_controls_verified" : "native_DesertMakeover_touch_event_and_completion_callback_verified",
            "exact_customization_receipt_and_native_owned_conservation_verified"
        } : reasons.Length == 0 ? new[] { "player_customization_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true, ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_calibration", PrimitiveKind = "customize_player",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification, RequestedEffect = PlayerCustomizationRequestedEffect(request),
            ObservedEffect = PlayerCustomizationObservedEffect(), BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[] { new SimulatedFactChange { Path = "player.customization", Before = request.CustomizationProjectionFingerprint, After = PlayerCustomizationObservedEffect() } }
        });
    }

    private static TrainingExecutionResult PlayerCustomizationBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "customize_player", PlayerCustomizationRequestedEffect(request), PlayerCustomizationObservedEffect(), reasons);

    private static string PlayerCustomizationRequestedEffect(TrainingExecutionRequest request) =>
        "mode=" + request.CustomizationMode + (request.CustomizationMode == "wizard_shrine"
            ? ";name=" + request.CustomizationName + ";hair=" + request.CustomizationHairStyleId
            : ";hat=" + request.CustomizationExpectedHatQid + ";shirt=" + request.CustomizationExpectedShirtQid + ";pants=" + request.CustomizationExpectedPantsQid);

    private static string PlayerCustomizationObservedEffect() =>
        "name=" + Game1.player.Name + ";gender=" + (Game1.player.IsMale ? "male" : "female") +
        ";skin=" + Game1.player.skin.Value + ";hair=" + Game1.player.hair.Value + ";accessory=" + Game1.player.accessory.Value +
        ";hat=" + (Game1.player.hat.Value?.QualifiedItemId ?? string.Empty) +
        ";shirt=" + (Game1.player.shirtItem.Value?.QualifiedItemId ?? string.Empty) +
        ";pants=" + (Game1.player.pantsItem.Value?.QualifiedItemId ?? string.Empty) +
        ";money=" + Game1.player.Money + ";daily_flag=" + Game1.player.activeDialogueEvents.ContainsKey("DesertMakeover") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") + ";event_up=" + Game1.eventUp;

    private static int?[] PlayerCustomizationSliders(TrainingExecutionRequest request) => new[]
    {
        request.CustomizationEyeHue, request.CustomizationEyeSaturation, request.CustomizationEyeValue,
        request.CustomizationHairHue, request.CustomizationHairSaturation, request.CustomizationHairValue
    };

    private static string PlayerCustomizationTimeoutReason(ActivePlayerCustomization active)
    {
        if (Game1.activeClickableMenu is not CharacterCustomization menu)
            return "player_customization_timeout:menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";event_up=" + Game1.eventUp + ";action_issued=" + active.ActionIssued;
        return "player_customization_timeout:eye_hsv=" + menu.eyeColorPicker.hueBar.value + "," +
            menu.eyeColorPicker.saturationBar.value + "," + menu.eyeColorPicker.valueBar.value +
            ";hair_hsv=" + menu.hairColorPicker.hueBar.value + "," + menu.hairColorPicker.saturationBar.value +
            "," + menu.hairColorPicker.valueBar.value + ";text_entered=" + active.TextEntered +
            ";ok_clicked=" + active.OkClicked;
    }
}
