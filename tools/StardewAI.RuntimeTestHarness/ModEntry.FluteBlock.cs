using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FluteBlockContract =
        "GameLocation.checkAction->Object.checkForAction_(O)464->CheckForActionOnFluteBlock->preservedParentSheetIndex_next_pitch->Game1.playSound_flute_pitch->shakeTimer_200->scaleY_1.3";

    private void StartFluteBlockTuning(PendingExecution pending)
    {
        var request = pending.Request;
        var generic = ValidateExecutionRequest(request);
        if (generic.Count > 0) { pending.Completion.SetResult(Blocked(request, generic.ToArray())); return; }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue || !request.FluteBlockCurrentPitch.HasValue || !request.FluteBlockNextPitch.HasValue ||
            !request.FluteBlockPitchMin.HasValue || !request.FluteBlockPitchMax.HasValue || !request.FluteBlockPitchStep.HasValue || !request.FluteBlockPitchStateCount.HasValue ||
            !request.FluteBlockExpectedShakeTimer.HasValue || !request.FluteBlockExpectedScaleY.HasValue || !request.FluteBlockExpectedLocationActionReturn.HasValue)
        { pending.Completion.SetResult(FluteBlockBlocked(request, "flute_block_typed_fields_required")); return; }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        { pending.Completion.SetResult(FluteBlockBlocked(request, "flute_block_player_or_menu_not_ready")); return; }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateFluteBlockTarget(location, target, stand, request, out var block);
        if (reasons.Length > 0) { pending.Completion.SetResult(FluteBlockBlocked(request, reasons)); return; }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null) { pending.Completion.SetResult(FluteBlockBlocked(request, "flute_block_path_unavailable:" + pathReason)); return; }
        nativeObjectInteractions.FluteBlock = new ActiveFluteBlock(pending, location, block!, target, stand, path, maxMovement);
    }

    private static string[] ValidateFluteBlockTarget(GameLocation location, Point target, Point stand, TrainingExecutionRequest request, out StardewObject? block)
    {
        var reasons = new List<string>();
        block = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) || item.GetType() != typeof(StardewObject) || item.bigCraftable.Value ||
            item.Name != "Flute Block" || item.Type != "Crafting" || item.ItemId != "464" || item.QualifiedItemId != "(O)464")
            reasons.Add("flute_block_exact_object_missing_or_drifted");
        else block = item;
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("flute_block_interaction_geometry_drifted");
        if (IsDestructiveObjectTrap(location, stand)) reasons.Add("flute_block_destructive_object_trap_preamble_blocked");
        var slot = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (slot is < 0 or > 11 || slot >= Game1.player.Items.Count) reasons.Add("flute_block_safe_toolbar_slot_drifted");
        else
        {
            var safeItem = Game1.player.Items[slot];
            if (request.FluteBlockSafeSlotKind switch { "empty" => safeItem is not null, "tool" => safeItem is not Tool, _ => true })
                reasons.Add("flute_block_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex) reasons.Add("flute_block_restore_slot_drifted");
        if (block is not null)
        {
            var livePitch = ComputeFluteBlockNextPitch(block.preservedParentSheetIndex.Value);
            if (block.preservedParentSheetIndex.Value != request.FluteBlockCurrentPitchRaw ||
                request.FluteBlockCurrentPitch != livePitch.Current || request.FluteBlockNextPitch != livePitch.Next ||
                request.FluteBlockPitchMin != 0 || request.FluteBlockPitchMax != 2400 ||
                request.FluteBlockPitchStep != 100 || request.FluteBlockPitchStateCount != 25 || request.FluteBlockExpectedShakeTimer != 200 ||
                Math.Abs(request.FluteBlockExpectedScaleY.GetValueOrDefault() - 1.3f) > 0.0001f || request.FluteBlockExpectedLocationActionReturn != true || request.FluteBlockSoundCue != "flute" ||
                request.ItemId != block.ItemId || request.QualifiedItemId != block.QualifiedItemId || request.LocationId != location.NameOrUniqueName ||
                request.TargetRuntimeType != typeof(StardewObject).FullName || request.InteractionKind != "location_object" || request.ExpectedActionType != "FluteBlock" || request.NativeContract != FluteBlockContract)
            {
                reasons.Add("flute_block_projection_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static (int Current, int Next) ComputeFluteBlockNextPitch(string? rawPitch)
    {
        _ = int.TryParse(rawPitch, out var current);
        var next = current switch { 2300 => 2400, 2400 => 0, _ => (current + 100) % 2400 };
        return (current, next);
    }

    private void TickFluteBlockTuning()
    {
        var active = nativeObjectInteractions.FluteBlock;
        if (active is null) return;
        var movement = AdvanceNativeObjectInteractionMovement(active, "flute_block", out var failure);
        if (movement == NativeObjectMovementStatus.Failed) { CompleteFluteBlock(active, false, failure); return; }
        if (movement == NativeObjectMovementStatus.Moving) return;
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) || !ReferenceEquals(current, active.Computer) ||
            current.preservedParentSheetIndex.Value != active.BeforePitchRaw || current.ItemId != active.BeforeItemId || current.QualifiedItemId != active.BeforeQualifiedItemId)
        { CompleteFluteBlock(active, false, "flute_block_object_or_pitch_drifted_while_moving"); return; }
        var safeItem = Game1.player.Items[active.SafeSlotIndex];
        if ((active.SafeSlotKind == "empty" && safeItem is not null) || (active.SafeSlotKind == "tool" && safeItem is not Tool) || IsDestructiveObjectTrap(active.Location, active.Stand))
        { CompleteFluteBlock(active, false, "flute_block_safe_context_drifted_while_moving"); return; }
        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null) { CompleteFluteBlock(active, false, "flute_block_active_object_selection_failed"); return; }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        active.NativeHandled = active.Location.checkAction(new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height), Game1.player);
        var verified = active.NativeHandled == active.ExpectedReturn && active.Computer.preservedParentSheetIndex.Value == active.ExpectedNextPitch.ToString() &&
            active.Computer.shakeTimer == active.ExpectedShake && Math.Abs(active.Computer.scale.Y - active.ExpectedScaleY) < 0.0001f &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var after) && ReferenceEquals(after, active.Computer) &&
            after.ItemId == active.BeforeItemId && after.QualifiedItemId == active.BeforeQualifiedItemId && Game1.activeClickableMenu is null;
        CompleteFluteBlock(active, verified, verified ? Array.Empty<string>() : new[] { "flute_block_native_receipt_mismatch" });
    }

    private void CompleteFluteBlock(ActiveFluteBlock active, bool verified, params string[] reasons)
    {
        StopAllMovement(); nativeObjectInteractions.FluteBlock = null; Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var verification = verified ? new[] { "shared_native_object_interaction_movement_reached_exact_adjacent_stand", "safe_toolbar_slot_disabled_held_object_sound_override", "one_native_GameLocation_checkAction_tuned_flute_block", "persistent_pitch_shake_and_scale_receipt_verified", "canonical_item_identity_unchanged", "selected_toolbar_slot_restored" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId, QueueId = active.Pending.Request.QueueId, QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash, OptionId = active.Pending.Request.OptionId, Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true, ActualTicks = active.ElapsedTicks, StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_evidence", PrimitiveKind = "tune_flute_block",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch", PrimitiveVerificationReasons = verification,
            RequestedEffect = $"pitch={active.ExpectedNextPitch};sound=flute;shake_timer=200;scale_y=1.3;selected_slot_restored=true",
            ObservedEffect = $"pitch={active.Computer.preservedParentSheetIndex.Value};shake_timer={active.Computer.shakeTimer};scale_y={active.Computer.scale.Y};native_handled={active.NativeHandled.ToString().ToLowerInvariant()};selected_slot={Game1.player.CurrentToolIndex}",
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[] { new SimulatedFactChange { Path = $"current_location.objects[{active.Target.X},{active.Target.Y}].preserved_parent_sheet_index", Before = active.BeforePitchRaw, After = active.Computer.preservedParentSheetIndex.Value }, new SimulatedFactChange { Path = "player.current_tool_index", Before = active.RestoreSlotIndex.ToString(), After = Game1.player.CurrentToolIndex.ToString() } }
        });
    }

    private static TrainingExecutionResult FluteBlockBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "tune_flute_block", $"pitch={request.FluteBlockNextPitch};sound=flute;shake_timer=200;scale_y=1.3", "flute_block_current_state=unverified", reasons);

    private sealed class ActiveFluteBlock : INativeObjectInteractionMovement
    {
        public ActiveFluteBlock(PendingExecution pending, GameLocation location, StardewObject computer, Point target, Point stand, List<Point> path, int maxMovement)
        {
            Pending = pending; Location = location; Computer = computer; Target = target; Stand = stand; Path = path; MaxMovementTiles = maxMovement;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value; SafeSlotKind = pending.Request.FluteBlockSafeSlotKind; RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            BeforePitchRaw = computer.preservedParentSheetIndex.Value; BeforeItemId = computer.ItemId; BeforeQualifiedItemId = computer.QualifiedItemId;
            ExpectedNextPitch = pending.Request.FluteBlockNextPitch!.Value; ExpectedShake = pending.Request.FluteBlockExpectedShakeTimer!.Value;
            ExpectedScaleY = pending.Request.FluteBlockExpectedScaleY!.Value; ExpectedReturn = pending.Request.FluteBlockExpectedLocationActionReturn!.Value;
            LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
        }
        public PendingExecution Pending { get; } public GameLocation Location { get; } public StardewObject Computer { get; } public Point Target { get; } public Point Stand { get; }
        public List<Point> Path { get; } public int MaxMovementTiles { get; } public int SafeSlotIndex { get; } public string SafeSlotKind { get; } public int RestoreSlotIndex { get; }
        public string BeforePitchRaw { get; } public string BeforeItemId { get; } public string BeforeQualifiedItemId { get; } public int ExpectedNextPitch { get; }
        public int ExpectedShake { get; } public float ExpectedScaleY { get; } public bool ExpectedReturn { get; } public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600; public int ElapsedTicks { get; set; } public int PathIndex { get; set; } public int StuckTicks { get; set; }
        public int MovementTiles { get; set; } public Vector2 LastPosition { get; set; } public Point LastObservedTile { get; set; } public bool NativeHandled { get; set; }
    }
}
