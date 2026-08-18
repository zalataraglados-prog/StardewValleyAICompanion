using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickForgeSafely()
    {
        if (activeForge is null) return;
        try { TickForge(); }
        catch (Exception ex)
        {
            Monitor.Log("Forge execution failed and was blocked: " + ex, LogLevel.Error);
            CompleteForgeBlocked(activeForge, "forge_item_exception:" + ex.GetType().Name);
        }
    }

    private void StartForge(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0) { pending.Completion.SetResult(Blocked(request, validation.ToArray())); return; }
        if (Game1.player is null || Game1.activeClickableMenu is not null ||
            string.IsNullOrWhiteSpace(request.ForgeCandidateId) || string.IsNullOrWhiteSpace(request.ForgeOperation) ||
            string.IsNullOrWhiteSpace(request.ForgeReason) || string.IsNullOrWhiteSpace(request.ForgeSourceId) ||
            request.ForgeSourceKind is not ("forge_action" or "mini_forge") ||
            string.IsNullOrWhiteSpace(request.LeftSourceId) || string.IsNullOrWhiteSpace(request.LeftStateJson) ||
            (!request.ForgeOperation.StartsWith("unforge_", StringComparison.Ordinal) &&
             (string.IsNullOrWhiteSpace(request.RightSourceId) || string.IsNullOrWhiteSpace(request.RightStateJson))) ||
            !request.InteractionTileX.HasValue || !request.InteractionTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ForgeShardCost.HasValue || !request.ForgeShardRefund.HasValue ||
            !request.ForgeShardCountBefore.HasValue || !request.TimesEnchantedBefore.HasValue ||
            !request.TimesEnchantedAfter.HasValue || string.IsNullOrWhiteSpace(request.ForgeOutputContractKind))
        {
            pending.Completion.SetResult(ForgeBlocked(request, "forge_item_typed_request_required")); return;
        }
        var location = Game1.currentLocation;
        var target = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (location.NameOrUniqueName != request.LocationId || Math.Abs(target.X - stand.X) + Math.Abs(target.Y - stand.Y) != 1 ||
            !ForgeSourceMatches(location, request, target))
        {
            pending.Completion.SetResult(ForgeBlocked(request, "forge_item_source_location_or_stand_drifted")); return;
        }
        var left = RebindForgeItem(request.LeftSourceId);
        var right = string.IsNullOrWhiteSpace(request.RightSourceId) ? null : RebindForgeItem(request.RightSourceId);
        if (left is null || ForgeStateJson(left) != request.LeftStateJson ||
            (!string.IsNullOrWhiteSpace(request.RightSourceId) && (right is null || ForgeStateJson(right) != request.RightStateJson)) ||
            Game1.player.Items.CountId("(O)848") != request.ForgeShardCountBefore ||
            (long)Game1.stats.Get("timesEnchanted") != request.TimesEnchantedBefore)
        {
            pending.Completion.SetResult(ForgeBlocked(request, "forge_item_input_shard_or_stat_projection_drifted")); return;
        }
        if (!ForgeNativePairValid(request.ForgeOperation, left, right))
        {
            pending.Completion.SetResult(ForgeBlocked(request, "forge_item_native_pair_no_longer_valid")); return;
        }
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand,
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null) { pending.Completion.SetResult(ForgeBlocked(request, "forge_item_path_unavailable:" + pathReason)); return; }
        activeForge = new ActiveForge(pending, location, target, stand, path, left, right)
        {
            BeforeCounts = CaptureForgeCounts(),
            CombinedComponents = left is CombinedRing combined ? combined.combinedRings.Cast<Item>().ToArray() : Array.Empty<Item>()
        };
    }

    private void TickForge()
    {
        var active = activeForge!;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location)) { CompleteForgeBlocked(active, "forge_item_location_changed"); return; }
        if (active.ElapsedTicks > 2400) { CompleteForgeBlocked(active, "forge_item_timeout"); return; }
        switch (active.Stage)
        {
            case ForgeStage.Move: TickForgeMove(active); break;
            case ForgeStage.Open: OpenForgeMenu(active); break;
            case ForgeStage.WaitMenu: WaitForgeMenu(active); break;
            case ForgeStage.LoadLeft: LoadForgeLeft(active); break;
            case ForgeStage.LoadRight: LoadForgeRight(active); break;
            case ForgeStage.Start: StartForgeOperation(active); break;
            case ForgeStage.WaitComplete: WaitForgeComplete(active); break;
            case ForgeStage.StoreOutput: StoreForgeOutput(active); break;
            case ForgeStage.Verify: CompleteForge(active); break;
        }
    }

    private void TickForgeMove(ActiveForge active)
    {
        var tile = Game1.player.TilePoint;
        if (tile == active.Stand) { StopAllMovement(); active.Stage = ForgeStage.Open; return; }
        if (active.PathIndex >= active.Path.Count) { CompleteForgeBlocked(active, "forge_item_path_exhausted"); return; }
        var next = active.Path[active.PathIndex];
        if (tile == next) { active.PathIndex++; return; }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next)) { CompleteForgeBlocked(active, "forge_item_dynamic_path_blocked"); return; }
        var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(tile, next)); MovePlayerForTick();
        if (Game1.player.TilePoint == next) active.PathIndex++;
        active.StuckTicks = moved ? 0 : active.StuckTicks + 1;
        if (active.StuckTicks > 45) CompleteForgeBlocked(active, "forge_item_movement_stuck");
    }

    private void OpenForgeMenu(ActiveForge active)
    {
        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var request = active.Pending.Request;
        bool handled;
        if (request.ForgeSourceKind == "forge_action")
        {
            if (!TryApplySmapiRightButtonOverride(true, out var reason)) { CompleteForgeBlocked(active, "forge_item_open_press_failed:" + reason); return; }
            handled = active.Location.checkAction(new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height), Game1.player);
            TryApplySmapiRightButtonOverride(false, out _);
        }
        else
        {
            handled = active.Location.objects.TryGetValue(active.Target.ToVector2(), out var value) &&
                value.QualifiedItemId == "(BC)MiniForge" && value.checkForAction(Game1.player);
        }
        if (!handled) { CompleteForgeBlocked(active, "forge_item_native_open_not_handled"); return; }
        active.Stage = ForgeStage.WaitMenu; active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForgeMenu(ActiveForge active)
    {
        if (Game1.activeClickableMenu is ForgeMenu menu)
        {
            active.Menu = menu; active.Stage = ForgeStage.LoadLeft; return;
        }
        if (active.ElapsedTicks - active.StageStartedAt > 120) CompleteForgeBlocked(active, "forge_item_native_menu_timeout");
    }

    private void LoadForgeLeft(ActiveForge active)
    {
        var reason = "not_clicked";
        if (!ClickForgeItemSource(active, active.Pending.Request.LeftSourceId, active.Left, out reason) || active.Menu?.heldItem != active.Left)
        { CompleteForgeBlocked(active, "forge_item_left_pickup_failed:" + reason); return; }
        ClickForgeComponent(active.Menu, active.Menu.leftIngredientSpot);
        if (active.Menu.heldItem is not null || active.Menu.leftIngredientSpot.item != active.Left)
        { CompleteForgeBlocked(active, "forge_item_left_slot_load_failed"); return; }
        active.Stage = ForgeStage.LoadRight;
    }

    private void LoadForgeRight(ActiveForge active)
    {
        var request = active.Pending.Request;
        if (request.ForgeOperation.StartsWith("unforge_", StringComparison.Ordinal)) { active.Stage = ForgeStage.Start; return; }
        var reason = "right_missing";
        if (active.Right is null || !ClickForgeItemSource(active, request.RightSourceId, active.Right, out reason) || active.Menu?.heldItem != active.Right)
        { CompleteForgeBlocked(active, "forge_item_right_pickup_failed:" + reason); return; }
        ClickForgeComponent(active.Menu, active.Menu.rightIngredientSpot);
        if (active.Menu.heldItem is not null || active.Menu.rightIngredientSpot.item != active.Right)
        { CompleteForgeBlocked(active, "forge_item_right_slot_load_failed"); return; }
        active.Stage = ForgeStage.Start;
    }

    private void StartForgeOperation(ActiveForge active)
    {
        var menu = active.Menu;
        if (menu is null || !ReferenceEquals(menu, Game1.activeClickableMenu)) { CompleteForgeBlocked(active, "forge_item_menu_lost_before_start"); return; }
        var component = active.Pending.Request.ForgeOperation.StartsWith("unforge_", StringComparison.Ordinal)
            ? menu.unforgeButton : menu.startTailoringButton;
        var point = component.bounds.Center;
        menu.receiveLeftClick(point.X, point.Y, playSound: false);
        if (!menu.IsBusy()) { CompleteForgeBlocked(active, "forge_item_native_start_rejected"); return; }
        active.NativeOperationStarted = true; active.Stage = ForgeStage.WaitComplete; active.StageStartedAt = active.ElapsedTicks;
    }

    private void WaitForgeComplete(ActiveForge active)
    {
        var menu = active.Menu;
        if (menu is null || !ReferenceEquals(menu, Game1.activeClickableMenu)) { CompleteForgeBlocked(active, "forge_item_menu_lost_during_operation"); return; }
        if (menu.IsBusy()) return;
        active.Result = menu.heldItem;
        active.Stage = ForgeStage.StoreOutput;
    }

    private void StoreForgeOutput(ActiveForge active)
    {
        var menu = active.Menu!;
        if (menu.heldItem is not null)
        {
            var slot = FindCraftedOutputInventorySlot(menu.heldItem);
            if (slot < 0 || slot >= menu.inventory.inventory.Count) { CompleteForgeBlocked(active, "forge_item_output_slot_unavailable"); return; }
            var point = menu.inventory.inventory[slot].bounds.Center;
            menu.receiveLeftClick(point.X, point.Y, playSound: false);
            if (menu.heldItem is not null) { CompleteForgeBlocked(active, "forge_item_output_inventory_click_failed"); return; }
        }
        if (!menu.readyToClose()) { CompleteForgeBlocked(active, "forge_item_menu_not_ready_to_close"); return; }
        menu.exitThisMenuNoSound();
        active.Stage = ForgeStage.Verify;
    }

    private void CompleteForge(ActiveForge active)
    {
        var request = active.Pending.Request;
        var shardAfter = Game1.player.Items.CountId("(O)848");
        var timesAfter = (long)Game1.stats.Get("timesEnchanted");
        var outputValid = ValidateForgeOutput(active, out var outputReason);
        var countsValid = ValidateForgeCounts(active, out var countReason);
        var verified = active.NativeOperationStarted && Game1.activeClickableMenu is null &&
            shardAfter == request.ForgeShardCountBefore - request.ForgeShardCost + request.ForgeShardRefund &&
            timesAfter == request.TimesEnchantedAfter && outputValid && countsValid;
        activeForge = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true, PrimitiveKind = "forge_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_forge_source_and_ForgeMenu_lifecycle_completed", "exact_live_input_sources_consumed_or_returned", "cinder_shard_and_timesEnchanted_delta_verified", request.ForgeOutputContractKind == "native_random_result_domain" ? "native_random_output_inside_complete_published_domain" : "exact_output_item_state_verified" }
                : new[] { outputReason, countReason, "shards_after=" + shardAfter, "times_enchanted_after=" + timesAfter },
            RequestedEffect = "native_forge_operation=" + request.ForgeOperation + ";forge_reason=" + request.ForgeReason,
            ObservedEffect = "output=" + (active.Result is null ? "multi_or_none" : ForgeStateJson(active.Result)) + ";shards_after=" + shardAfter + ";times_enchanted_after=" + timesAfter,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"), ActualTicks = active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "forge_item_post_state_mismatch" }
        });
    }

    private static bool ValidateForgeOutput(ActiveForge active, out string reason)
    {
        var request = active.Pending.Request;
        if (request.ForgeOperation == "unforge_combined_ring")
        {
            var valid = active.CombinedComponents.All(component => PlayerOwnsReference(component));
            reason = valid ? "combined_ring_components_returned" : "combined_ring_component_missing"; return valid;
        }
        if (active.Result is null) { reason = "forge_result_missing"; return false; }
        if (request.ForgeOutputContractKind != "native_random_result_domain")
        {
            var valid = ForgeStateJson(active.Result) == request.ExpectedOutputStateJson;
            reason = valid ? "exact_output_state_match" : "exact_output_state_mismatch"; return valid;
        }
        if (active.Result is not Tool tool) { reason = "random_output_not_tool"; return false; }
        using var document = JsonDocument.Parse(request.RandomOutcomeContractJson);
        var root = document.RootElement;
        if (request.ForgeOperation == "diamond_forge")
        {
            var allowed = root.GetProperty("allowed_added_runtime_types").EnumerateArray().Select(value => value.GetString() ?? "").ToHashSet(StringComparer.Ordinal);
            var expected = root.GetProperty("add_distinct_count").GetInt32();
            var before = active.BeforeForgeTypes;
            var added = tool.enchantments.Where(value => value.IsForge() && value is not DiamondEnchantment && !before.Contains(value.GetType().Name)).Select(value => value.GetType().Name).Distinct().ToArray();
            var valid = tool.hasEnchantmentOfType<DiamondEnchantment>() && added.Length == expected && added.All(allowed.Contains);
            reason = valid ? "diamond_result_inside_domain" : "diamond_result_outside_domain"; return valid;
        }
        var excluded = root.GetProperty("excluded_previous_runtime_types").EnumerateArray().Select(value => value.GetString() ?? "").ToHashSet(StringComparer.Ordinal);
        var allowedLevels = root.GetProperty("allowed").EnumerateArray().ToDictionary(value => value.GetProperty("runtime_type").GetString() ?? "",
            value => (Min: value.GetProperty("min_level").GetInt32(), Max: value.GetProperty("max_level").GetInt32()), StringComparer.Ordinal);
        var secondary = tool.enchantments.Where(value => value.IsSecondaryEnchantment() && value is not GalaxySoulEnchantment).ToArray();
        var domainValid = secondary.Length is >= 1 and <= 2 && secondary.All(value => allowedLevels.TryGetValue(value.GetType().Name, out var range) && value.Level >= range.Min && value.Level <= range.Max && !excluded.Contains(value.GetType().Name));
        reason = domainValid ? "dragon_tooth_result_inside_domain" : "dragon_tooth_result_outside_domain"; return domainValid;
    }

    private static bool ValidateForgeCounts(ActiveForge active, out string reason)
    {
        var after = CaptureForgeCounts();
        var expected = new Dictionary<string, int>(active.BeforeCounts, StringComparer.Ordinal);
        void Add(string id, int delta) { if (id == "(O)848") return; expected[id] = expected.GetValueOrDefault(id) + delta; }
        var operation = active.Pending.Request.ForgeOperation;
        if (operation == "combine_rings") { Add(active.Left.QualifiedItemId, -1); Add(active.Right!.QualifiedItemId, -1); Add(active.Result!.QualifiedItemId, 1); }
        else if (operation == "unforge_combined_ring") { Add(active.Left.QualifiedItemId, -1); foreach (var item in active.CombinedComponents) Add(item.QualifiedItemId, 1); }
        else if (operation == "unforge_weapon")
        {
            if (!string.IsNullOrWhiteSpace(active.BeforeAppearance)) Add(active.BeforeAppearance, 1);
        }
        else Add(active.Right!.QualifiedItemId, -1);
        var valid = expected.Where(pair => pair.Key != "(O)848").All(pair => after.GetValueOrDefault(pair.Key) == pair.Value) &&
            after.Where(pair => pair.Key != "(O)848").All(pair => expected.GetValueOrDefault(pair.Key) == pair.Value);
        reason = valid ? "qualified_item_counts_match" : "qualified_item_counts_mismatch"; return valid;
    }

    private static bool ForgeNativePairValid(string operation, Item left, Item? right) => operation switch
    {
        "unforge_weapon" => left is MeleeWeapon weapon && (weapon.GetTotalForgeLevels() > 0 || weapon.appearance.Value is not null),
        "unforge_combined_ring" => left is CombinedRing,
        "combine_rings" => left is Ring ring && right is Ring other && ring.CanCombine(other),
        _ => left is Tool tool && right is not null && tool.CanForge(right)
    };

    private static bool ForgeSourceMatches(GameLocation location, TrainingExecutionRequest request, Point target)
    {
        if (request.ForgeSourceKind == "forge_action")
            return request.ForgeSourceId == "forge-action:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y &&
                location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") == "Forge";
        return request.ForgeSourceId == "mini-forge:" + location.NameOrUniqueName + ":" + target.X + "," + target.Y &&
            location.objects.TryGetValue(target.ToVector2(), out var value) && value.QualifiedItemId == "(BC)MiniForge";
    }

    private static Item? RebindForgeItem(string sourceId)
    {
        if (sourceId.StartsWith("inventory:", StringComparison.Ordinal) && int.TryParse(sourceId[10..], out var slot) && slot >= 0 && slot < Game1.player.Items.Count) return Game1.player.Items[slot];
        if (sourceId == "equipped:left_ring") return Game1.player.leftRing.Value;
        if (sourceId == "equipped:right_ring") return Game1.player.rightRing.Value;
        return null;
    }

    private static bool ClickForgeItemSource(ActiveForge active, string sourceId, Item expected, out string reason)
    {
        var menu = active.Menu!;
        if (sourceId.StartsWith("inventory:", StringComparison.Ordinal) && int.TryParse(sourceId[10..], out var slot) && slot >= 0 && slot < menu.inventory.inventory.Count)
        { ClickForgeComponent(menu, menu.inventory.inventory[slot]); reason = "inventory_clicked"; return menu.heldItem == expected; }
        var index = sourceId == "equipped:left_ring" ? 0 : sourceId == "equipped:right_ring" ? 1 : -1;
        if (index >= 0) { ClickForgeComponent(menu, menu.equipmentIcons[index]); reason = "equipment_clicked"; return menu.heldItem == expected; }
        reason = "source_id_invalid"; return false;
    }

    private static void ClickForgeComponent(ForgeMenu menu, ClickableComponent component)
    { var point = component.bounds.Center; menu.receiveLeftClick(point.X, point.Y, playSound: false); }

    private static Dictionary<string, int> CaptureForgeCounts()
    {
        var items = Game1.player.Items.Where(item => item is not null).Cast<Item>().Concat(new Item?[] { Game1.player.leftRing.Value, Game1.player.rightRing.Value }.Where(item => item is not null).Cast<Item>());
        return items.GroupBy(item => item.QualifiedItemId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Sum(item => item.Stack), StringComparer.Ordinal);
    }

    private static bool PlayerOwnsReference(Item expected) => Game1.player.Items.Any(item => ReferenceEquals(item, expected)) || ReferenceEquals(Game1.player.leftRing.Value, expected) || ReferenceEquals(Game1.player.rightRing.Value, expected);

    private static string ForgeStateJson(Item item) => JsonSerializer.Serialize(new
    {
        qualified_item_id = item.QualifiedItemId, runtime_type = item.GetType().FullName, stack = item.Stack, quality = item.Quality,
        enchantments = item is Tool tool ? tool.enchantments.Select(value => new { runtime_type = value.GetType().Name, level = value.Level }).ToArray() : Array.Empty<object>(),
        previous_enchantments = item is Tool previous ? previous.previousEnchantments.ToArray() : Array.Empty<string>(),
        total_forge_levels = item is Tool forge ? forge.GetTotalForgeLevels() : 0,
        total_unforge_levels = item is Tool unforge ? unforge.GetTotalForgeLevels(for_unforge: true) : 0,
        max_forge_levels = item is Tool maximum ? maximum.GetMaxForges() : 0,
        weapon_appearance = item is MeleeWeapon weapon ? weapon.appearance.Value ?? string.Empty : string.Empty,
        weapon_type = item is MeleeWeapon typed ? typed.type.Value : -1,
        weapon_item_level = item is MeleeWeapon leveled ? leveled.getItemLevel() : -1,
        combined_ring_ids = item is CombinedRing combined ? combined.combinedRings.Select(value => value.QualifiedItemId).ToArray() : Array.Empty<string>()
    });

    private void CompleteForgeBlocked(ActiveForge active, params string[] reasons)
    {
        StopAllMovement(); TryApplySmapiRightButtonOverride(false, out _);
        if (Game1.activeClickableMenu is ForgeMenu menu)
        {
            if (menu.heldItem is not null) menu.emergencyShutDown(); else if (menu.readyToClose()) menu.exitThisMenuNoSound();
        }
        activeForge = null; active.Pending.Completion.SetResult(ForgeBlocked(active.Pending.Request, reasons));
    }

    private static TrainingExecutionResult ForgeBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "forge_item", "native_forge_operation=" + request.ForgeOperation,
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") + ";shards=" + (Game1.player?.Items.CountId("(O)848") ?? -1), reasons);

    private enum ForgeStage { Move, Open, WaitMenu, LoadLeft, LoadRight, Start, WaitComplete, StoreOutput, Verify }

    private sealed class ActiveForge
    {
        public ActiveForge(PendingExecution pending, GameLocation location, Point target, Point stand, List<Point> path, Item left, Item? right)
        {
            Pending = pending; Location = location; Target = target; Stand = stand; Path = path; Left = left; Right = right;
            LastPosition = Game1.player.Position;
            BeforeForgeTypes = left is Tool tool
                ? tool.enchantments.Where(value => value.IsForge()).Select(value => value.GetType().Name).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            BeforeAppearance = left is MeleeWeapon weapon ? weapon.appearance.Value ?? string.Empty : string.Empty;
        }
        public PendingExecution Pending { get; } public GameLocation Location { get; } public Point Target { get; } public Point Stand { get; }
        public List<Point> Path { get; } public Item Left { get; } public Item? Right { get; } public ForgeMenu? Menu { get; set; }
        public Item? Result { get; set; } public Item[] CombinedComponents { get; set; } = Array.Empty<Item>();
        public Dictionary<string, int> BeforeCounts { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> BeforeForgeTypes { get; }
        public string BeforeAppearance { get; }
        public ForgeStage Stage { get; set; } public int ElapsedTicks { get; set; } public int StageStartedAt { get; set; }
        public int PathIndex { get; set; } public int StuckTicks { get; set; } public Vector2 LastPosition { get; set; }
        public bool NativeOperationStarted { get; set; } public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
