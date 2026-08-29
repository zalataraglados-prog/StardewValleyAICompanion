using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FishPondManagementNativeContract =
        "GameLocation.checkAction(right_click)->FishPond.doAction->PondQueryMenu.receiveLeftClick->changeNettingButton|emptyButton->yesButton->FishPond.ClearPond";
    private static readonly FieldInfo? PondQueryBoundPondField = typeof(PondQueryMenu)
        .GetField("_pond", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? PondQueryConfirmingEmptyField = typeof(PondQueryMenu)
        .GetField("confirmingEmpty", BindingFlags.Instance | BindingFlags.NonPublic);

    private TrainingExecutionResult ExecuteSetupFishPondManagement(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
            return FishPondManagementBlocked(request, "fish_pond_management_fixture_top_left_required");

        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var selected = FindFishPondFixturePlacement(farm, new Point(request.TargetTileX.Value, request.TargetTileY.Value));
        if (!selected.HasValue)
            return FishPondManagementBlocked(request, "fish_pond_management_fixture_no_legal_placement");
        var pond = new FishPond(selected.Value.ToVector2());
        if (!farm.buildStructure(pond, selected.Value.ToVector2(), Game1.player, skipSafetyChecks: false))
            return FishPondManagementBlocked(request, "fish_pond_management_fixture_placement_rejected");

        pond.daysOfConstructionLeft.Value = 0;
        var fish = ItemRegistry.Create(string.IsNullOrWhiteSpace(request.FishTypeItemId) ? "(O)698" : request.FishTypeItemId);
        pond.fishType.Value = fish.ItemId;
        pond.UpdateMaximumOccupancy();
        pond.currentOccupants.Value = Math.Max(1, Math.Min(3, pond.maxOccupants.Value));
        pond.daysSinceSpawn.Value = 7;
        pond.lastUnlockedPopulationGate.Value = Math.Max(0, pond.maxOccupants.Value - 1);
        pond.neededItem.Value = ItemRegistry.Create("(O)72");
        pond.neededItemCount.Value = 2;
        pond.hasCompletedRequest.Value = true;
        pond.goldenAnimalCracker.Value = true;
        pond.isPlayingGoldenCrackerAnimation.Value = true;
        pond.hasSpawnedFish.Value = true;
        pond.sign.Value = ItemRegistry.Create<StardewObject>("(O)698");
        pond.nettingStyle.Value = 2;
        pond.overrideWaterColor.Value = Color.CornflowerBlue;
        pond.output.Value = null;

        var target = new Point(pond.tileX.Value, pond.tileY.Value);
        var moved = MoveFixtureFarmerToFarmAdjacent(target, out var stand, out var moveReason);
        var safeSlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null || Game1.player.Items[index] is Tool);
        if (safeSlot < 0 || safeSlot >= Math.Min(12, Game1.player.Items.Count) ||
            (Game1.player.Items[safeSlot] is not null && Game1.player.Items[safeSlot] is not Tool))
            safeSlot = -1;
        var verified = moved && safeSlot >= 0 && farm.buildings.Contains(pond);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_fish_pond_management",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_fish_pond_management_fixture_ready", "stand_tile=" + stand.X + "," + stand.Y, "safe_slot=" + safeSlot }
                : new[] { safeSlot < 0 ? "fish_pond_management_fixture_safe_slot_unavailable" : moveReason },
            RequestedEffect = "farm.fish_pond.management_status=ready",
            ObservedEffect = FishPondManagementObservedEffect(pond),
            BlockReasons = verified ? Array.Empty<string>() : new[] { safeSlot < 0 ? "fish_pond_management_fixture_safe_slot_unavailable" : moveReason },
            ChangedFacts = verified
                ? new[] { new SimulatedFactChange { Path = "farm.buildings[" + pond.tileX.Value + "," + pond.tileY.Value + "].fish_pond.management_status", Before = "missing", After = "ready" } }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private void StartFishPondManagement(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.BuildingTileX.HasValue || !request.BuildingTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue ||
            !request.ExpectedFishCount.HasValue || !request.ExpectedFishCountAfter.HasValue ||
            !request.ExpectedMaximumOccupantsBefore.HasValue || !request.ExpectedMaximumOccupantsAfter.HasValue ||
            !request.ExpectedLastUnlockedPopulationGateBefore.HasValue || !request.ExpectedLastUnlockedPopulationGateAfter.HasValue ||
            !request.ExpectedDaysSinceSpawnBefore.HasValue || !request.ExpectedDaysSinceSpawnAfter.HasValue ||
            !request.ExpectedNeededItemCountBefore.HasValue || !request.ExpectedNeededItemCountAfter.HasValue ||
            !request.ExpectedHasCompletedRequestBefore.HasValue || !request.ExpectedHasCompletedRequestAfter.HasValue ||
            !request.ExpectedGoldenAnimalCrackerBefore.HasValue || !request.ExpectedGoldenAnimalCrackerAfter.HasValue ||
            !request.ExpectedHasSpawnedFishBefore.HasValue || !request.ExpectedHasSpawnedFishAfter.HasValue ||
            !request.ExpectedNettingStyleBefore.HasValue || !request.ExpectedNettingStyleAfter.HasValue ||
            !request.ExpectedFishDebrisCount.HasValue || !request.ExpectedOverrideWaterColorPackedBefore.HasValue ||
            request.ManagementOperation is not ("cycle_netting" or "empty_pond") ||
            string.IsNullOrWhiteSpace(request.FishPondManagementReason) || string.IsNullOrWhiteSpace(request.FishTypeItemId) ||
            !string.Equals(request.NativeContract, FishPondManagementNativeContract, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_typed_projection_required"));
            return;
        }
        if (request.ManagementOperation == "empty_pond" && request.ConfirmEmptyPond != true)
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_empty_explicit_confirmation_required"));
            return;
        }
        if (activeFishPondManagement is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_player_or_menu_busy"));
            return;
        }

        var farm = Game1.getFarm();
        if (!ReferenceEquals(Game1.currentLocation, farm) || !ReferenceEquals(Game1.player.currentLocation, farm))
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_player_not_on_farm"));
            return;
        }
        var pond = farm.buildings.OfType<FishPond>().FirstOrDefault(candidate =>
            candidate.tileX.Value == request.BuildingTileX && candidate.tileY.Value == request.BuildingTileY);
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (pond is null || pond.GetType() != typeof(FishPond) ||
            !string.Equals(request.TargetRuntimeType, typeof(FishPond).FullName, StringComparison.Ordinal) ||
            !pond.occupiesTile(target.ToVector2()) || !AreAdjacent(target, stand) || !IsTileOnMap(farm, stand) ||
            !IsTileWalkable(farm, stand) || IsTileOccupiedByCharacter(farm, stand))
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_target_or_geometry_drifted"));
            return;
        }
        var safeSlot = request.SafeSlotIndex.Value;
        if (safeSlot is < 0 or > 11 || safeSlot >= Game1.player.Items.Count ||
            (Game1.player.Items[safeSlot] is not null && Game1.player.Items[safeSlot] is not Tool) ||
            request.RestoreSlotIndex.Value != Game1.player.CurrentToolIndex)
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_safe_or_restore_slot_drifted", pond));
            return;
        }
        var before = ReadFishPondManagementState(pond);
        if (!FishPondManagementBeforeMatches(before, request))
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_state_drifted", pond));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(farm, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FishPondManagementBlocked(request, "fish_pond_management_path_unavailable:" + pathReason, pond));
            return;
        }
        activeFishPondManagement = new ActiveFishPondManagement(
            pending, farm, pond, target, stand, path, maxMovementTiles, before, farm.debris.ToHashSet());
    }

    private void TickFishPondManagement()
    {
        var active = activeFishPondManagement;
        if (active is null)
            return;
        var movement = AdvanceNativeObjectInteractionMovement(active, "fish_pond_management", out var failure);
        if (movement == NativeObjectMovementStatus.Failed)
        {
            CompleteFishPondManagement(active, false, failure);
            return;
        }
        if (movement == NativeObjectMovementStatus.Moving)
            return;
        if (!active.Location.buildings.Contains(active.Pond) ||
            !ReadFishPondManagementState(active.Pond).Equals(active.Before))
        {
            CompleteFishPondManagement(active, false, "fish_pond_management_state_drifted_while_moving");
            return;
        }

        StopAllMovement();
        Game1.player.CurrentToolIndex = active.Pending.Request.SafeSlotIndex!.Value;
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        NativeRightClickEdgePatch.Arm();
        bool handled;
        bool rightClickObserved;
        try
        {
            handled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            rightClickObserved = NativeRightClickEdgePatch.WasObserved;
        }
        finally
        {
            NativeRightClickEdgePatch.Clear();
        }
        var openedMenu = Game1.activeClickableMenu;
        var boundPond = openedMenu is PondQueryMenu openedPondMenu
            ? PondQueryBoundPondField?.GetValue(openedPondMenu)
            : null;
        if (!handled || openedMenu is not PondQueryMenu menu || !ReferenceEquals(boundPond, active.Pond))
        {
            CompleteFishPondManagement(active, false,
                "fish_pond_management_native_menu_open_mismatch:handled=" + handled.ToString().ToLowerInvariant() +
                ";right_click=" + rightClickObserved.ToString().ToLowerInvariant() +
                ";menu=" + (openedMenu?.GetType().FullName ?? "none") +
                ";bound_field=" + (PondQueryBoundPondField is not null).ToString().ToLowerInvariant() +
                ";bound_match=" + ReferenceEquals(boundPond, active.Pond).ToString().ToLowerInvariant() +
                ";target_occupied=" + active.Pond.occupiesTile(active.Target.ToVector2()).ToString().ToLowerInvariant() +
                ";active_object=" + (Game1.player.ActiveObject?.QualifiedItemId ?? "none"));
            return;
        }

        if (active.Pending.Request.ManagementOperation == "cycle_netting")
        {
            menu.receiveLeftClick(menu.changeNettingButton.bounds.Center.X, menu.changeNettingButton.bounds.Center.Y);
            var styleVerified = active.Pond.nettingStyle.Value == active.Pending.Request.ExpectedNettingStyleAfter &&
                FishPondManagementStateEqualsExceptNetting(active.Before, ReadFishPondManagementState(active.Pond));
            menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
            CompleteFishPondManagement(active, styleVerified && Game1.activeClickableMenu is null,
                styleVerified ? "native_PondQueryMenu_cycled_netting_and_preserved_economic_state" : "fish_pond_netting_post_state_mismatch");
            return;
        }

        menu.receiveLeftClick(menu.emptyButton.bounds.Center.X, menu.emptyButton.bounds.Center.Y);
        if (PondQueryConfirmingEmptyField?.GetValue(menu) is not true || menu.yesButton is null)
        {
            CompleteFishPondManagement(active, false, "fish_pond_empty_confirmation_menu_mismatch");
            return;
        }
        menu.receiveLeftClick(menu.yesButton.bounds.Center.X, menu.yesButton.bounds.Center.Y);
        var after = ReadFishPondManagementState(active.Pond);
        var newDebris = active.Location.debris.Where(debris => !active.DebrisBefore.Contains(debris)).ToArray();
        var matchingDebris = newDebris.Count(debris =>
            string.Equals(DebrisQualifiedItemId(debris), active.Pending.Request.ExpectedFishDebrisQualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
            (debris.item?.Stack ?? Math.Max(1, debris.Chunks.Count)) == 1);
        var clearVerified = Game1.activeClickableMenu is null &&
            FishPondClearAfterMatches(active, after) &&
            newDebris.Length == active.Pending.Request.ExpectedFishDebrisCount &&
            matchingDebris == active.Pending.Request.ExpectedFishDebrisCount;
        CompleteFishPondManagement(active, clearVerified,
            clearVerified ? "native_PondQueryMenu_confirmed_ClearPond_exact_reset_preservation_and_fish_debris" : "fish_pond_empty_post_state_mismatch");
    }

    private static bool FishPondManagementBeforeMatches(FishPondManagementState state, TrainingExecutionRequest request) =>
        state.FishTypeItemId == request.FishTypeItemId && state.FishCount == request.ExpectedFishCount &&
        state.MaximumOccupants == request.ExpectedMaximumOccupantsBefore &&
        state.LastUnlockedPopulationGate == request.ExpectedLastUnlockedPopulationGateBefore &&
        state.DaysSinceSpawn == request.ExpectedDaysSinceSpawnBefore &&
        state.NeededItemQualifiedItemId == request.ExpectedNeededItemQualifiedItemIdBefore &&
        state.NeededItemCount == request.ExpectedNeededItemCountBefore &&
        BoolInt(state.HasCompletedRequest) == request.ExpectedHasCompletedRequestBefore &&
        BoolInt(state.GoldenAnimalCracker) == request.ExpectedGoldenAnimalCrackerBefore &&
        BoolInt(state.HasSpawnedFish) == request.ExpectedHasSpawnedFishBefore &&
        state.NettingStyle == request.ExpectedNettingStyleBefore &&
        state.SignQualifiedItemId == request.ExpectedSignQualifiedItemIdBefore &&
        state.OutputQualifiedItemId == request.ExpectedOutputQualifiedItemIdBefore &&
        state.OutputQualifiedItemId.Length == 0 && state.OverrideWaterColorPacked == request.ExpectedOverrideWaterColorPackedBefore;

    private static bool FishPondManagementStateEqualsExceptNetting(
        FishPondManagementState before,
        FishPondManagementState after) =>
        before with { NettingStyle = after.NettingStyle } == after;

    private static bool FishPondClearAfterMatches(ActiveFishPondManagement active, FishPondManagementState after)
    {
        var request = active.Pending.Request;
        return after.FishTypeItemId.Length == 0 && after.FishCount == request.ExpectedFishCountAfter &&
            after.MaximumOccupants == request.ExpectedMaximumOccupantsAfter &&
            after.LastUnlockedPopulationGate == request.ExpectedLastUnlockedPopulationGateAfter &&
            after.DaysSinceSpawn == request.ExpectedDaysSinceSpawnAfter &&
            after.NeededItemQualifiedItemId.Length == 0 && after.NeededItemCount == request.ExpectedNeededItemCountAfter &&
            BoolInt(after.HasCompletedRequest) == request.ExpectedHasCompletedRequestAfter &&
            BoolInt(after.GoldenAnimalCracker) == request.ExpectedGoldenAnimalCrackerAfter &&
            !after.GoldenAnimalCrackerAnimation && BoolInt(after.HasSpawnedFish) == request.ExpectedHasSpawnedFishAfter &&
            after.NettingStyle == request.ExpectedNettingStyleAfter &&
            after.SignQualifiedItemId == request.ExpectedSignQualifiedItemIdBefore &&
            ReferenceEquals(after.SignReference, active.Before.SignReference) &&
            after.OutputQualifiedItemId == request.ExpectedOutputQualifiedItemIdBefore &&
            ReferenceEquals(after.OutputReference, active.Before.OutputReference) &&
            after.OverrideWaterColorPacked == Color.White.PackedValue && after.SeedOffset is >= 0 and <= 999;
    }

    private void CompleteFishPondManagement(ActiveFishPondManagement active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        NativeRightClickEdgePatch.Clear();
        if (Game1.activeClickableMenu is PondQueryMenu menu && menu.readyToClose())
            menu.exitThisMenuNoSound();
        Game1.player.CurrentToolIndex = active.Pending.Request.RestoreSlotIndex!.Value;
        activeFishPondManagement = null;
        var request = active.Pending.Request;
        var after = ReadFishPondManagementState(active.Pond);
        var verificationReasons = verified
            ? new[] { "shared_BFS_reached_exact_pond_edge_stand", "native_right_click_opened_exact_bound_PondQueryMenu", reasons.FirstOrDefault() ?? "native_fish_pond_management_applied", "selected_toolbar_slot_restored" }
            : reasons.Length == 0 ? new[] { "fish_pond_management_post_state_mismatch" } : reasons;
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
            TrainingImpactScope = "player_command_executor_evaluation_only",
            PrimitiveKind = "manage_fish_pond:" + request.ManagementOperation,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = FishPondManagementRequestedEffect(request),
            ObservedEffect = FishPondManagementObservedEffect(active.Pond),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = FishPondManagementPath(active) + ".netting_style", Before = active.Before.NettingStyle.ToString(CultureInfo.InvariantCulture), After = after.NettingStyle.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = FishPondManagementPath(active) + ".fish_type_item_id", Before = active.Before.FishTypeItemId, After = after.FishTypeItemId },
                new SimulatedFactChange { Path = FishPondManagementPath(active) + ".fish_count", Before = active.Before.FishCount.ToString(CultureInfo.InvariantCulture), After = after.FishCount.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "farm.debris.count", Before = active.DebrisBefore.Count.ToString(CultureInfo.InvariantCulture), After = active.Location.debris.Count.ToString(CultureInfo.InvariantCulture) }
            }
        });
    }

    private static TrainingExecutionResult FishPondManagementBlocked(
        TrainingExecutionRequest request,
        string reason,
        FishPond? pond = null) =>
        BlockedWithPrimitive(request, "manage_fish_pond:" + request.ManagementOperation,
            FishPondManagementRequestedEffect(request),
            pond is null ? "fish_pond=unavailable" : FishPondManagementObservedEffect(pond), reason);

    private static string FishPondManagementRequestedEffect(TrainingExecutionRequest request) =>
        request.ManagementOperation == "cycle_netting"
            ? "fish_pond.netting_style=" + request.ExpectedNettingStyleAfter + ";economic_state_unchanged=true"
            : "fish_pond.fish_type=empty;fish_count=0;fish_debris_count=" + request.ExpectedFishDebrisCount + ";clear_reset_and_preservation_verified=true";

    private static string FishPondManagementObservedEffect(FishPond pond)
    {
        var state = ReadFishPondManagementState(pond);
        return "fish_type_item_id=" + state.FishTypeItemId + ";fish_count=" + state.FishCount +
            ";maximum_occupants=" + state.MaximumOccupants + ";last_gate=" + state.LastUnlockedPopulationGate +
            ";days_since_spawn=" + state.DaysSinceSpawn + ";needed_item=" + state.NeededItemQualifiedItemId +
            ";needed_count=" + state.NeededItemCount + ";completed_request=" + state.HasCompletedRequest.ToString().ToLowerInvariant() +
            ";golden_cracker=" + state.GoldenAnimalCracker.ToString().ToLowerInvariant() +
            ";netting_style=" + state.NettingStyle + ";sign=" + state.SignQualifiedItemId +
            ";output=" + state.OutputQualifiedItemId + ";water_color=" + state.OverrideWaterColorPacked +
            ";seed_offset=" + state.SeedOffset;
    }

    private static FishPondManagementState ReadFishPondManagementState(FishPond pond) =>
        new(
            pond.fishType.Value ?? string.Empty,
            pond.FishCount,
            pond.maxOccupants.Value,
            pond.lastUnlockedPopulationGate.Value,
            pond.daysSinceSpawn.Value,
            pond.neededItem.Value?.QualifiedItemId ?? string.Empty,
            pond.neededItemCount.Value,
            pond.hasCompletedRequest.Value,
            pond.goldenAnimalCracker.Value,
            pond.isPlayingGoldenCrackerAnimation.Value,
            pond.hasSpawnedFish.Value,
            pond.nettingStyle.Value,
            pond.seedOffset.Value,
            pond.overrideWaterColor.Value.PackedValue,
            pond.sign.Value?.QualifiedItemId ?? string.Empty,
            pond.sign.Value,
            pond.output.Value?.QualifiedItemId ?? string.Empty,
            pond.output.Value);

    private static int BoolInt(bool value) => value ? 1 : 0;

    private static string FishPondManagementPath(ActiveFishPondManagement active) =>
        "farm.buildings[" + active.Pond.tileX.Value + "," + active.Pond.tileY.Value + "].fish_pond";

    private sealed class ActiveFishPondManagement : INativeObjectInteractionMovement
    {
        public ActiveFishPondManagement(PendingExecution pending, GameLocation location, FishPond pond,
            Point target, Point stand, List<Point> path, int maxMovementTiles,
            FishPondManagementState before, HashSet<Debris> debrisBefore)
        {
            Pending = pending;
            Location = location;
            Pond = pond;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            Before = before;
            DebrisBefore = debrisBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public FishPond Pond { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public FishPondManagementState Before { get; }
        public HashSet<Debris> DebrisBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
    }

    private sealed record FishPondManagementState(
        string FishTypeItemId,
        int FishCount,
        int MaximumOccupants,
        int LastUnlockedPopulationGate,
        int DaysSinceSpawn,
        string NeededItemQualifiedItemId,
        int NeededItemCount,
        bool HasCompletedRequest,
        bool GoldenAnimalCracker,
        bool GoldenAnimalCrackerAnimation,
        bool HasSpawnedFish,
        int NettingStyle,
        int SeedOffset,
        uint OverrideWaterColorPacked,
        string SignQualifiedItemId,
        StardewObject? SignReference,
        string OutputQualifiedItemId,
        Item? OutputReference);
}
