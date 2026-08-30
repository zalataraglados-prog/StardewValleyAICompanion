using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeGeodeNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Process)->GeodeMenu_inventory_click->GeodeMenu_geodeSpot_click->2700ms_native_animation->inventory_receipt";
    private static readonly HashSet<string> RuntimeLockedBaseGeodeIds = new(StringComparer.Ordinal)
    {
        "(O)275", "(O)535", "(O)536", "(O)537", "(O)749", "(O)791", "(O)MysteryBox", "(O)GoldenMysteryBox"
    };

    private void StartGeodeProcessing(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0) { pending.Completion.SetResult(Blocked(request, reasons.ToArray())); return; }
        var accepted = ParseGeodeAcceptedOutputs(request.GeodeAcceptedOutputsJson);
        if (!GeodeRequestIsTyped(request, accepted))
        {
            pending.Completion.SetResult(GeodeBlocked(request, "geode_processing_complete_typed_request_required")); return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(GeodeBlocked(request, "geode_processing_player_menu_or_event_not_ready")); return;
        }
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var liveReasons = ValidateGeodeLiveState(location, target, stand, request);
        if (liveReasons.Length > 0) { pending.Completion.SetResult(GeodeBlocked(request, liveReasons)); return; }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null) { pending.Completion.SetResult(GeodeBlocked(request, "geode_processing_path_unavailable:" + pathReason)); return; }
        activeGeodeProcessing = new ActiveGeodeProcessing(pending, location, target, stand, path, maxMovementTiles, accepted);
    }

    private static bool GeodeRequestIsTyped(TrainingExecutionRequest request, List<GeodeAcceptedOutput> accepted) =>
        !string.IsNullOrWhiteSpace(request.GeodePurpose) && RuntimeLockedBaseGeodeIds.Contains(request.GeodeQualifiedItemId) &&
        request.GeodeSlotIndex is >= 0 && request.GeodeInputQuality is >= 0 && request.GeodeStackBefore is > 0 && request.GeodeFreeSlotsBefore is >= 0 &&
        request.GeodeMoneyBefore is >= 25 && request.GeodePriceGold == 25 && request.GeodesCrackedBefore is >= 0 &&
        request.MysteryBoxesOpenedBefore is >= 0 && request.GoldenCoconutCrackedBefore.HasValue &&
        request.GoldenWalnutsBefore is >= 0 && request.GoldenWalnutsFoundBefore is >= 0 && request.GeodeArchaeologyFoundCount is >= 0 &&
        request.GeodeSaveIdHalf.HasValue && request.GeodePlayerIdHalf.HasValue && !string.IsNullOrWhiteSpace(request.GeodeSeason) &&
        request.GeodeDeepestMineLevel.HasValue && request.GeodeSkill1Level.HasValue && request.GeodeFarmingMasteryUnlocked.HasValue &&
        request.GeodeQiBeansRuleActive.HasValue && request.GeodeGotMysteryBookMailBefore.HasValue && request.GeodeArtifactFoundMailBefore.HasValue &&
        request.GeodePredictionKind is "exact" or "complete_shared_rng_crop_family" or "first_golden_coconut_mutex_contingent" &&
        accepted.Count > 0 && request.GeodeProjectionFingerprint.Length == 64 && request.GeodeActionToken == "Blacksmith" &&
        request.NativeContract == RuntimeGeodeNativeContract && request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue;

    private static string[] ValidateGeodeLiveState(GameLocation location, Point target, Point stand,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var slot = request.GeodeSlotIndex!.Value;
        var item = slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var clint = location.characters.FirstOrDefault(npc => npc.Name == "Clint");
        if (location.NameOrUniqueName != "Blacksmith" || request.LocationId != "Blacksmith" ||
            location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") != request.GeodeActionRaw ||
            request.GeodeActionRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() != "Blacksmith" ||
            !AreAdjacent(target, stand) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand))
            reasons.Add("geode_processing_location_endpoint_or_geometry_drifted");
        if (clint is null) reasons.Add("geode_processing_clint_not_at_blacksmith");
        if (Game1.player.toolBeingUpgraded.Value is not null && Game1.player.daysLeftForToolUpgrade.Value <= 0)
            reasons.Add("geode_processing_completed_tool_upgrade_intercepts_counter");
        if (item is null || item.QualifiedItemId != request.GeodeQualifiedItemId || item.Quality != request.GeodeInputQuality ||
            item.Stack != request.GeodeStackBefore ||
            !Utility.IsGeode(item) || !RuntimeLockedBaseGeodeIds.Contains(item.QualifiedItemId))
            reasons.Add("geode_processing_inventory_slot_or_input_drifted");
        if (Game1.player.Money != request.GeodeMoneyBefore || request.GeodePriceGold != 25 || Game1.player.Money < 25 ||
            Game1.player.freeSpotsInInventory() != request.GeodeFreeSlotsBefore ||
            (Game1.player.freeSpotsInInventory() < 1 && request.GeodeStackBefore != 1))
            reasons.Add("geode_processing_money_or_output_capacity_drifted");
        if (Game1.stats.GeodesCracked != request.GeodesCrackedBefore ||
            Game1.stats.Get("MysteryBoxesOpened") != request.MysteryBoxesOpenedBefore ||
            Game1.netWorldState.Value.GoldenCoconutCracked != request.GoldenCoconutCrackedBefore ||
            Game1.netWorldState.Value.GoldenWalnuts != request.GoldenWalnutsBefore ||
            Game1.netWorldState.Value.GoldenWalnutsFound != request.GoldenWalnutsFoundBefore ||
            Game1.player.archaeologyFound.Length != request.GeodeArchaeologyFoundCount)
            reasons.Add("geode_processing_counter_or_golden_coconut_state_drifted");
        if ((long)(Game1.uniqueIDForThisGame / 2) != request.GeodeSaveIdHalf ||
            Game1.player.UniqueMultiplayerID / 2 != request.GeodePlayerIdHalf ||
            Game1.season.ToString().ToLowerInvariant() != request.GeodeSeason ||
            Game1.player.deepestMineLevel != request.GeodeDeepestMineLevel ||
            Game1.player.GetUnmodifiedSkillLevel(1) != request.GeodeSkill1Level ||
            (Game1.player.stats.Get(StatKeys.Mastery(0)) != 0) != request.GeodeFarmingMasteryUnlocked ||
            Game1.player.team.SpecialOrderRuleActive("DROP_QI_BEANS") != request.GeodeQiBeansRuleActive ||
            Game1.player.mailReceived.Contains("GotMysteryBook") != request.GeodeGotMysteryBookMailBefore ||
            Game1.player.hasOrWillReceiveMail("artifactFound") != request.GeodeArtifactFoundMailBefore)
            reasons.Add("geode_processing_output_predictor_context_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickGeodeProcessing()
    {
        var active = activeGeodeProcessing;
        if (active is null) return;
        active.ElapsedTicks++; active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks) { CompleteGeodeProcessing(active, false, "geode_processing_timeout:" + GeodeObservedEffect()); return; }
        var request = active.Pending.Request;
        if (!active.CounterActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "geode_processing", out var failure);
            if (movement == NativeObjectMovementStatus.Failed) { CompleteGeodeProcessing(active, false, failure); return; }
            if (movement == NativeObjectMovementStatus.Moving) return;
            var reasons = ValidateGeodeLiveState(active.Location, active.Target, active.Stand, request);
            if (reasons.Length > 0) { CompleteGeodeProcessing(active, false, reasons); return; }
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
            active.Location.checkAction(new xTile.Dimensions.Location(active.Target.X, active.Target.Y), Game1.viewport, Game1.player);
            active.CounterActionIssued = true; active.StageTicks = 0; return;
        }
        if (!active.ProcessAnswered)
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion || active.Location.lastQuestionKey != "Blacksmith")
            {
                if (active.StageTicks > 180) CompleteGeodeProcessing(active, false, "geode_processing_blacksmith_question_timeout");
                return;
            }
            var process = dialogue.responses?.FirstOrDefault(response => response.responseKey == "Process");
            if (process is null || !active.Location.answerDialogue(process))
            { CompleteGeodeProcessing(active, false, "geode_processing_native_process_response_failed"); return; }
            active.ProcessAnswered = true; active.StageTicks = 0; return;
        }
        if (Game1.activeClickableMenu is not GeodeMenu menu)
        {
            if (active.CloseClicked && GeodeReceiptMatches(active)) CompleteGeodeProcessing(active, true);
            else if (active.StageTicks > 240) CompleteGeodeProcessing(active, false, "geode_processing_menu_open_or_close_timeout");
            return;
        }
        if (!active.InventoryClicked)
        {
            var slot = request.GeodeSlotIndex!.Value;
            if (slot >= menu.inventory.inventory.Count) { CompleteGeodeProcessing(active, false, "geode_processing_native_inventory_slot_missing"); return; }
            var point = menu.inventory.inventory[slot].bounds.Center;
            menu.receiveLeftClick(point.X, point.Y);
            if (menu.heldItem?.QualifiedItemId != request.GeodeQualifiedItemId)
            { CompleteGeodeProcessing(active, false, "geode_processing_native_inventory_pickup_failed"); return; }
            active.InventoryClicked = true; active.StageTicks = 0; return;
        }
        if (!active.GeodeSpotClicked)
        {
            var point = menu.geodeSpot.bounds.Center; menu.receiveLeftClick(point.X, point.Y);
            active.GeodeSpotClicked = true; active.StageTicks = 0; return;
        }
        if (!active.AnimationStarted)
        {
            if (menu.geodeAnimationTimer > 0 || menu.waitingForServerResponse) { active.AnimationStarted = true; active.StageTicks = 0; }
            else if (active.StageTicks > 180) { CompleteGeodeProcessing(active, false, "geode_processing_native_animation_did_not_start"); }
            return;
        }
        if (menu.geodeAnimationTimer > 0 || menu.waitingForServerResponse || menu.geodeTreasure is not null || menu.geodeSpot.item is not null) return;
        if (!active.HeldStackReturned && menu.heldItem is not null)
        {
            var returnSlot = -1;
            for (var index = 0; index < menu.inventory.inventory.Count && index < Game1.player.Items.Count; index++)
            {
                if (Game1.player.Items[index] is null) { returnSlot = index; break; }
            }
            if (returnSlot < 0)
            { CompleteGeodeProcessing(active, false, "geode_processing_no_empty_native_return_slot"); return; }
            var point = menu.inventory.inventory[returnSlot].bounds.Center;
            menu.receiveLeftClick(point.X, point.Y); active.HeldStackReturned = menu.heldItem is null;
            if (!active.HeldStackReturned)
            { CompleteGeodeProcessing(active, false, "geode_processing_native_remaining_stack_return_failed"); return; }
            active.StageTicks = 0; return;
        }
        active.HeldStackReturned = true;
        if (!GeodeReceiptMatches(active))
        { CompleteGeodeProcessing(active, false, "geode_processing_native_receipt_mismatch:" + GeodeReceiptDiagnostic(active)); return; }
        if (!active.CloseClicked)
        {
            if (!menu.readyToClose() || menu.okButton is null) { if (active.StageTicks > 240) CompleteGeodeProcessing(active, false, "geode_processing_menu_not_ready_to_close"); return; }
            var point = menu.okButton.bounds.Center; menu.receiveLeftClick(point.X, point.Y); active.CloseClicked = true; active.StageTicks = 0;
        }
    }

    private static bool GeodeReceiptMatches(ActiveGeodeProcessing active)
    {
        var request = active.Pending.Request;
        if (Game1.player.Money != request.GeodeMoneyBefore - 25 ||
            Game1.stats.GeodesCracked != request.GeodesCrackedBefore + 1 ||
            Game1.stats.Get("MysteryBoxesOpened") != request.MysteryBoxesOpenedBefore +
                (request.GeodeQualifiedItemId.Contains("MysteryBox", StringComparison.Ordinal) ? 1 : 0)) return false;
        var after = GeodeInventoryCounts();
        foreach (var output in active.Accepted)
        {
            var persistentStack = output.InventoryPersists ? output.Stack : 0;
            var keys = active.InventoryBefore.Keys.Concat(after.Keys)
                .Concat(new[] { GeodeInventoryKey(request.GeodeQualifiedItemId, request.GeodeInputQuality!.Value), GeodeInventoryKey(output.QualifiedItemId, output.Quality) })
                .Distinct(StringComparer.Ordinal);
            var matches = keys.All(key => after.GetValueOrDefault(key) == active.InventoryBefore.GetValueOrDefault(key) -
                (key == GeodeInventoryKey(request.GeodeQualifiedItemId, request.GeodeInputQuality!.Value) ? 1 : 0) +
                (key == GeodeInventoryKey(output.QualifiedItemId, output.Quality) ? persistentStack : 0));
            if (!matches) continue;
            var expectedMail = output.ExpectedMailAdditions.ToHashSet(StringComparer.Ordinal);
            var actualMail = Game1.player.mailReceived.Where(flag => !active.MailBefore.Contains(flag)).ToHashSet(StringComparer.Ordinal);
            if (!actualMail.SetEquals(expectedMail)) continue;
            var expectedGolden = request.GoldenCoconutCrackedBefore == true || output.QualifiedItemId == "(O)73";
            if (Game1.netWorldState.Value.GoldenCoconutCracked != expectedGolden) continue;
            var walnutDelta = output.QualifiedItemId == "(O)73" && request.GoldenWalnutsFoundBefore < 130 ? output.Stack : 0;
            if (Game1.netWorldState.Value.GoldenWalnuts != request.GoldenWalnutsBefore + walnutDelta ||
                Game1.netWorldState.Value.GoldenWalnutsFound != request.GoldenWalnutsFoundBefore + walnutDelta) continue;
            if (!GeodePickupCountersMatch(active, output)) continue;
            active.ActualOutput = output; return true;
        }
        return false;
    }

    private static bool GeodePickupCountersMatch(ActiveGeodeProcessing active, GeodeAcceptedOutput output)
    {
        var item = ItemRegistry.Create(output.QualifiedItemId, output.Stack, output.Quality);
        var mineralDelta = item is StardewValley.Object { Category: -2 } || item is StardewValley.Object { Type: "Minerals" } ? 1 : 0;
        var artifactDelta = item is StardewValley.Object { Type: "Arch" } ? 1 : 0;
        if (GeodeMineralFoundCount(output.QualifiedItemId) != active.MineralFoundBefore.GetValueOrDefault(output.QualifiedItemId) + mineralDelta ||
            GeodeArtifactFoundCount(output.QualifiedItemId) != active.ArtifactFoundBefore.GetValueOrDefault(output.QualifiedItemId) + artifactDelta)
            return false;
        return Game1.stats.StoneGathered == active.StoneGatheredBefore + (output.QualifiedItemId == "(O)390" ? 1u : 0u) &&
            Game1.stats.CopperFound == active.CopperFoundBefore + (output.QualifiedItemId == "(O)378" ? (uint)output.Stack : 0u) &&
            Game1.stats.IronFound == active.IronFoundBefore + (output.QualifiedItemId == "(O)380" ? (uint)output.Stack : 0u) &&
            Game1.stats.GoldFound == active.GoldFoundBefore + (output.QualifiedItemId == "(O)384" ? (uint)output.Stack : 0u) &&
            Game1.stats.IridiumFound == active.IridiumFoundBefore + (output.QualifiedItemId == "(O)386" ? (uint)output.Stack : 0u);
    }

    private static int GeodeMineralFoundCount(string qualifiedItemId)
    {
        var itemId = ItemRegistry.Create(qualifiedItemId).ItemId;
        return Game1.player.mineralsFound.TryGetValue(itemId, out var count) ? count : 0;
    }

    private static int GeodeArtifactFoundCount(string qualifiedItemId)
    {
        var itemId = ItemRegistry.Create(qualifiedItemId).ItemId;
        return Game1.player.archaeologyFound.TryGetValue(itemId, out var counts) && counts.Length > 0 ? counts[0] : 0;
    }

    private void CompleteGeodeProcessing(ActiveGeodeProcessing active, bool verified, params string[] reasons)
    {
        StopAllMovement(); activeGeodeProcessing = null;
        var request = active.Pending.Request;
        var verification = verified ? new[] { "shared_bfs_reached_exact_Blacksmith_counter",
            "native_Blacksmith_Process_and_GeodeMenu_click_sequence_completed", "one_geode_money_stats_mail_team_and_output_conservation_verified" }
            : reasons.Length == 0 ? new[] { "geode_processing_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true, ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration_and_strategy_processing_feedback", PrimitiveKind = "crack_geode",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch", PrimitiveVerificationReasons = verification,
            RequestedEffect = GeodeRequestedEffect(request), ObservedEffect = GeodeObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[] { new SimulatedFactChange { Path = "player.geode_processing",
                Before = request.GeodeProjectionFingerprint, After = GeodeObservedEffect() } }
        });
    }

    private static TrainingExecutionResult GeodeBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "crack_geode", GeodeRequestedEffect(request), GeodeObservedEffect(), reasons);

    private static string GeodeRequestedEffect(TrainingExecutionRequest request) =>
        "input=" + request.GeodeQualifiedItemId + ";prediction=" + request.GeodePredictionKind +
        ";expected=" + request.GeodeExpectedOutputQid + ";money_delta=-25";

    private static string GeodeObservedEffect() => "money=" + Game1.player.Money + ";geodes=" + Game1.stats.GeodesCracked +
        ";mystery=" + Game1.stats.Get("MysteryBoxesOpened") + ";golden_coconut=" + Game1.netWorldState.Value.GoldenCoconutCracked +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");

    private static string GeodeReceiptDiagnostic(ActiveGeodeProcessing active)
    {
        static string Counts(Dictionary<string, int> values) => string.Join(",",
            values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        var accepted = string.Join(",", active.Accepted.Select(row =>
            row.QualifiedItemId + "|q=" + row.Quality + "=" + row.Stack + ":persist=" + row.InventoryPersists +
            ":mail=" + string.Join("+", row.ExpectedMailAdditions)));
        var mailAfter = Game1.player.mailReceived.Where(flag => !active.MailBefore.Contains(flag))
            .OrderBy(value => value, StringComparer.Ordinal);
        return "before[" + Counts(active.InventoryBefore) + "]_after[" + Counts(GeodeInventoryCounts()) +
            "]_accepted[" + accepted + "]_mail_added[" + string.Join(",", mailAfter) + "]";
    }

    private static Dictionary<string, int> GeodeInventoryCounts() => Game1.player.Items.Where(item => item is not null)
        .GroupBy(item => GeodeInventoryKey(item!.QualifiedItemId, item.Quality), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Sum(item => item!.Stack), StringComparer.Ordinal);

    private static string GeodeInventoryKey(string qid, int quality) => qid + "|q=" + quality;

    private static List<GeodeAcceptedOutput> ParseGeodeAcceptedOutputs(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind != JsonValueKind.Array ? new() : document.RootElement.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => new GeodeAcceptedOutput(
                    row.TryGetProperty("qualified_item_id", out var qid) ? qid.GetString() ?? string.Empty : string.Empty,
                    row.TryGetProperty("stack", out var stack) ? stack.GetInt32() : 0,
                    row.TryGetProperty("quality", out var quality) ? quality.GetInt32() : 0,
                    row.TryGetProperty("set_flag_on_pickup", out var flag) ? flag.GetString() ?? string.Empty : string.Empty,
                    !row.TryGetProperty("inventory_persists", out var persists) || persists.GetBoolean(),
                    row.TryGetProperty("pickup_effect_kind", out var effect) ? effect.GetString() ?? "inventory_item" : "inventory_item",
                    row.TryGetProperty("expected_mail_additions", out var mail) && mail.ValueKind == JsonValueKind.Array
                        ? mail.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                            .Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                        : Array.Empty<string>()))
                .Where(row => row.QualifiedItemId.Length > 0 && row.Stack > 0).ToList();
        }
        catch (JsonException) { return new(); }
    }

    private static HashSet<string> ParseGeodeExpectedMail(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind != JsonValueKind.Array ? new(StringComparer.Ordinal) :
                document.RootElement.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.String)
                    .Select(row => row.GetString()).Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!).ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException) { return new(StringComparer.Ordinal); }
    }
}
