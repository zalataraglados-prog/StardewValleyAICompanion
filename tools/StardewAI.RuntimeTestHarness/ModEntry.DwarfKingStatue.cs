using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string DwarfKingStatueQualifiedItemId = "(BC)StatueOfTheDwarfKing";
    private const string DwarfKingStatueNativeContract = "Object.checkForAction_StatueOfTheDwarfKing->ChooseFromIconsMenu(dwarfStatue)->receiveLeftClick_exact_offered_icon->Farmer.applyBuff(dwarfStatue_N)";

    private void StartDwarfKingStatuePowerChoice(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!request.DwarfStatuePowerId.HasValue || !request.DwarfStatueMenuIndex.HasValue ||
            !request.DwarfStatueDaysPlayed.HasValue || !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(DwarfKingStatueBlocked(request, "dwarf_king_statue_typed_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(DwarfKingStatueBlocked(request, "dwarf_king_statue_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateDwarfKingStatueTarget(location, target, stand, request, out var statue, out var offeredPowerIds);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(DwarfKingStatueBlocked(request, reasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(DwarfKingStatueBlocked(request, "dwarf_king_statue_path_unavailable:" + pathReason));
            return;
        }

        activeDwarfKingStatueChoice = new ActiveDwarfKingStatueChoice(
            pending, location, statue!, target, stand, path, offeredPowerIds, maxMovementTiles);
    }

    private static string[] ValidateDwarfKingStatueTarget(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out StardewObject? statue,
        out int[] offeredPowerIds)
    {
        var reasons = new List<string>();
        statue = null;
        offeredPowerIds = DwarfKingStatueOffers();
        if (Game1.player.stats.Get(StatKeys.Mastery(3)) < 1)
        {
            reasons.Add("dwarf_king_statue_mining_mastery_required");
        }
        if (DwarfKingActiveBuffIds().Length != 0)
        {
            reasons.Add("dwarf_king_statue_power_already_chosen_today");
        }
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) ||
            item.GetType() != typeof(StardewObject) ||
            !string.Equals(item.QualifiedItemId, DwarfKingStatueQualifiedItemId, StringComparison.Ordinal) ||
            !item.bigCraftable.Value)
        {
            reasons.Add("dwarf_king_statue_exact_object_missing_or_drifted");
        }
        else
        {
            statue = item;
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("dwarf_king_statue_interaction_geometry_drifted");
        }
        var powerId = request.DwarfStatuePowerId ?? -1;
        var expectedIndex = Array.IndexOf(offeredPowerIds, powerId);
        if (expectedIndex < 0 || request.DwarfStatueMenuIndex != expectedIndex)
        {
            reasons.Add("dwarf_king_statue_selected_power_not_exactly_offered");
        }
        if (!string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(StardewObject).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, DwarfKingStatueQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(request.DwarfStatuePowerSource, "small_model_exact_offered_choice", StringComparison.Ordinal) ||
            !string.Equals(request.DwarfStatueBuffId, "dwarfStatue_" + powerId, StringComparison.Ordinal) ||
            !string.Equals(request.DwarfStatueOfferedPowerIdsCsv, string.Join(",", offeredPowerIds), StringComparison.Ordinal) ||
            request.DwarfStatueDaysPlayed != Game1.stats.DaysPlayed ||
            !string.Equals(request.ExpectedMenuTypeAfter, "ChooseFromIconsMenu", StringComparison.Ordinal) ||
            !string.Equals(request.InteractionKind, "location_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "StatueOfTheDwarfKing", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, DwarfKingStatueNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("dwarf_king_statue_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickDwarfKingStatuePowerChoice()
    {
        var active = activeDwarfKingStatueChoice;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_timeout");
            return;
        }

        if (active.IconClicked)
        {
            var selectedBuffApplied = Game1.player.hasBuff(active.ExpectedBuffId);
            var activeDwarfBuffs = DwarfKingActiveBuffIds();
            if (selectedBuffApplied && activeDwarfBuffs.SequenceEqual(new[] { active.ExpectedBuffId }) &&
                Game1.activeClickableMenu is null)
            {
                CompleteDwarfKingStatueChoice(active, true);
            }
            else if (active.ElapsedTicks - active.IconClickedAtTick > 180)
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_selected_buff_or_menu_close_receipt_mismatch");
            }
            return;
        }

        if (active.ActionIssued)
        {
            if (Game1.activeClickableMenu is not ChooseFromIconsMenu menu ||
                !ReferenceEquals(menu.sourceObject, active.Statue) ||
                menu.icons.Count != 2 ||
                !menu.icons.Select(icon => icon.name).SequenceEqual(active.OfferedPowerIds.Select(value => value.ToString())))
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_native_menu_offer_drifted");
                return;
            }
            var targetIcon = menu.icons[active.ExpectedMenuIndex];
            var center = targetIcon.bounds.Center;
            menu.receiveLeftClick(center.X, center.Y);
            active.IconClicked = true;
            active.IconClickedAtTick = active.ElapsedTicks;
            if (!Game1.player.hasBuff(active.ExpectedBuffId))
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_native_icon_click_did_not_apply_selected_buff");
            }
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
        }
        if (active.MovementTiles > active.MaxMovementTiles)
        {
            CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_movement_budget_exceeded");
            return;
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (playerTile == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_dynamic_path_blocked");
                return;
            }
            var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(playerTile, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            active.StuckTicks = moved ? 0 : active.StuckTicks + 1;
            if (active.StuckTicks > 45)
            {
                CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        active.NativeHandled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        if (!active.NativeHandled)
        {
            CompleteDwarfKingStatueChoice(active, false, "dwarf_king_statue_native_action_not_handled");
        }
    }

    private void CompleteDwarfKingStatueChoice(ActiveDwarfKingStatueChoice active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeDwarfKingStatueChoice = null;
        var request = active.Pending.Request;
        var activeBuffs = DwarfKingActiveBuffIds();
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_adjacent_stand",
                "native_Object_checkForAction_opened_exact_daily_ChooseFromIconsMenu",
                "native_ChooseFromIconsMenu_receiveLeftClick_applied_selected_day_buff",
                "exactly_one_selected_dwarf_statue_buff_observed_after_menu_close"
            }
            : reasons.Length == 0 ? new[] { "dwarf_king_statue_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "strategy_value_and_executor_calibration",
            PrimitiveKind = "choose_dwarf_statue_power",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = DwarfKingStatueRequestedEffect(request),
            ObservedEffect = "active_dwarf_statue_buffs=" + string.Join(",", activeBuffs) +
                ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.dwarf_king_statue_power.has_active_dwarf_statue_buff", Before = "false", After = verified ? "true" : (activeBuffs.Length > 0).ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "current_location.dwarf_king_statue_power.active_dwarf_statue_buff.buff_id", Before = string.Empty, After = string.Join(",", activeBuffs) }
            }
        });
    }

    private static TrainingExecutionResult DwarfKingStatueBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "choose_dwarf_statue_power", DwarfKingStatueRequestedEffect(request),
            "active_dwarf_statue_buffs=" + string.Join(",", DwarfKingActiveBuffIds()) + ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"), reasons);

    private static string DwarfKingStatueRequestedEffect(TrainingExecutionRequest request) =>
        "player.has_buff[dwarfStatue_" + request.DwarfStatuePowerId + "]=true;valid_until_day_end=true";

    private static int[] DwarfKingStatueOffers()
    {
        var random = Utility.CreateRandom(Game1.stats.DaysPlayed * 77, Game1.uniqueIDForThisGame);
        var first = random.Next(5);
        int second;
        do
        {
            second = random.Next(5);
        }
        while (second == first);
        return new[] { first, second };
    }

    private static string[] DwarfKingActiveBuffIds() => Game1.player.buffs.AppliedBuffs.Keys
        .Where(id => id.StartsWith("dwarfStatue_", StringComparison.Ordinal))
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    private sealed class ActiveDwarfKingStatueChoice
    {
        public ActiveDwarfKingStatueChoice(PendingExecution pending, GameLocation location, StardewObject statue,
            Point target, Point stand, List<Point> path, int[] offeredPowerIds, int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Statue = statue;
            Target = target;
            Stand = stand;
            Path = path;
            OfferedPowerIds = offeredPowerIds;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewObject Statue { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int[] OfferedPowerIds { get; }
        public int ExpectedMenuIndex => Pending.Request.DwarfStatueMenuIndex!.Value;
        public string ExpectedBuffId => Pending.Request.DwarfStatueBuffId;
        public int MaxMovementTiles { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int IconClickedAtTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; }
        public bool NativeHandled { get; set; }
        public bool IconClicked { get; set; }
    }
}
