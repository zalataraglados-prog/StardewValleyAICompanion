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
    private void StartNoteBlockTuning(PendingExecution pending, GameLocation location, StardewObject block, Point target, Point stand,
        NoteBlockProfile profile, string safeSlotKind, int expectedNextValue, int expectedShake, float expectedScaleY, bool expectedReturn,
        Func<TrainingExecutionRequest, string[], TrainingExecutionResult> blocked)
    {
        var maxMovement = Math.Clamp(pending.Request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(blocked(pending.Request, new[] { profile.ReasonPrefix + "_path_unavailable:" + pathReason }));
            return;
        }
        nativeObjectInteractions.NoteBlock = new ActiveNoteBlock(pending, location, block, target, stand, path, maxMovement,
            profile, safeSlotKind, expectedNextValue, expectedShake, expectedScaleY, expectedReturn);
    }

    private static List<string> ValidateNoteBlockTarget(GameLocation location, Point target, Point stand, TrainingExecutionRequest request,
        NoteBlockProfile profile, string safeSlotKind, out StardewObject? block)
    {
        var reasons = new List<string>();
        block = null;
        if (!location.objects.TryGetValue(target.ToVector2(), out var item) || item.GetType() != typeof(StardewObject) || item.bigCraftable.Value ||
            item.Name != profile.Name || item.Type != "Crafting" || item.ItemId != profile.ItemId || item.QualifiedItemId != profile.QualifiedItemId)
            reasons.Add(profile.ReasonPrefix + "_exact_object_missing_or_drifted");
        else block = item;
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add(profile.ReasonPrefix + "_interaction_geometry_drifted");
        if (IsDestructiveObjectTrap(location, stand)) reasons.Add(profile.ReasonPrefix + "_destructive_object_trap_preamble_blocked");
        var slot = request.SafeSlotIndex.GetValueOrDefault(-1);
        if (slot is < 0 or > 11 || slot >= Game1.player.Items.Count) reasons.Add(profile.ReasonPrefix + "_safe_toolbar_slot_drifted");
        else
        {
            var safeItem = Game1.player.Items[slot];
            if (safeSlotKind switch { "empty" => safeItem is not null, "tool" => safeItem is not Tool, _ => true })
                reasons.Add(profile.ReasonPrefix + "_safe_toolbar_slot_drifted");
        }
        if (request.RestoreSlotIndex is < 0 or > 11 || request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add(profile.ReasonPrefix + "_restore_slot_drifted");
        if (block is not null && (request.ItemId != block.ItemId || request.QualifiedItemId != block.QualifiedItemId ||
            request.LocationId != location.NameOrUniqueName || request.TargetRuntimeType != typeof(StardewObject).FullName ||
            request.InteractionKind != "location_object" || request.ExpectedActionType != profile.ExpectedActionType || request.NativeContract != profile.NativeContract))
            reasons.Add(profile.ReasonPrefix + "_projection_drifted");
        return reasons;
    }

    private void TickNoteBlockTuning()
    {
        var active = nativeObjectInteractions.NoteBlock;
        if (active is null) return;
        var movement = AdvanceNativeObjectInteractionMovement(active, active.Profile.ReasonPrefix, out var failure);
        if (movement == NativeObjectMovementStatus.Failed) { CompleteNoteBlock(active, false, failure); return; }
        if (movement == NativeObjectMovementStatus.Moving) return;
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) || !ReferenceEquals(current, active.Block) ||
            current.preservedParentSheetIndex.Value != active.BeforeValueRaw || current.ItemId != active.BeforeItemId || current.QualifiedItemId != active.BeforeQualifiedItemId)
        { CompleteNoteBlock(active, false, active.Profile.ReasonPrefix + "_object_or_value_drifted_while_moving"); return; }
        var safeItem = Game1.player.Items[active.SafeSlotIndex];
        if ((active.SafeSlotKind == "empty" && safeItem is not null) || (active.SafeSlotKind == "tool" && safeItem is not Tool) || IsDestructiveObjectTrap(active.Location, active.Stand))
        { CompleteNoteBlock(active, false, active.Profile.ReasonPrefix + "_safe_context_drifted_while_moving"); return; }
        Game1.player.CurrentToolIndex = active.SafeSlotIndex;
        if (Game1.player.ActiveObject is not null) { CompleteNoteBlock(active, false, active.Profile.ReasonPrefix + "_active_object_selection_failed"); return; }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        active.NativeHandled = active.Location.checkAction(new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height), Game1.player);
        var verified = active.NativeHandled == active.ExpectedReturn && active.Block.preservedParentSheetIndex.Value == active.ExpectedNextValue.ToString() &&
            active.Block.shakeTimer == active.ExpectedShake && Math.Abs(active.Block.scale.Y - active.ExpectedScaleY) < 0.0001f &&
            active.Location.objects.TryGetValue(active.Target.ToVector2(), out var after) && ReferenceEquals(after, active.Block) &&
            after.ItemId == active.BeforeItemId && after.QualifiedItemId == active.BeforeQualifiedItemId && Game1.activeClickableMenu is null;
        CompleteNoteBlock(active, verified, verified ? Array.Empty<string>() : new[] { active.Profile.ReasonPrefix + "_native_receipt_mismatch" });
    }

    private void CompleteNoteBlock(ActiveNoteBlock active, bool verified, params string[] reasons)
    {
        StopAllMovement(); nativeObjectInteractions.NoteBlock = null; Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        var verification = verified ? new[]
        {
            "shared_native_object_interaction_movement_reached_exact_adjacent_stand", "safe_toolbar_slot_selected_for_native_note_block_interaction",
            "one_native_GameLocation_checkAction_tuned_" + active.Profile.ReasonPrefix, "persistent_value_shake_and_scale_receipt_verified",
            "canonical_item_identity_unchanged", "selected_toolbar_slot_restored"
        } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId, QueueId = active.Pending.Request.QueueId, QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash, OptionId = active.Pending.Request.OptionId, Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true, ActualTicks = active.ElapsedTicks, StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_evidence", PrimitiveKind = active.Profile.PrimitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch", PrimitiveVerificationReasons = verification,
            RequestedEffect = $"{active.Profile.ValueLabel}={active.ExpectedNextValue};sound={active.SoundCue};shake_timer={active.ExpectedShake};scale_y={active.ExpectedScaleY};selected_slot_restored=true",
            ObservedEffect = $"{active.Profile.ValueLabel}={active.Block.preservedParentSheetIndex.Value};shake_timer={active.Block.shakeTimer};scale_y={active.Block.scale.Y};native_handled={active.NativeHandled.ToString().ToLowerInvariant()};selected_slot={Game1.player.CurrentToolIndex}",
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = $"current_location.objects[{active.Target.X},{active.Target.Y}].preserved_parent_sheet_index", Before = active.BeforeValueRaw, After = active.Block.preservedParentSheetIndex.Value },
                new SimulatedFactChange { Path = "player.current_tool_index", Before = active.RestoreSlotIndex.ToString(), After = Game1.player.CurrentToolIndex.ToString() }
            }
        });
    }

    private sealed record NoteBlockProfile(string ReasonPrefix, string PrimitiveKind, string ValueLabel,
        string Name, string ItemId, string QualifiedItemId, string ExpectedActionType, string NativeContract);

    private sealed class ActiveNoteBlock : INativeObjectInteractionMovement
    {
        public ActiveNoteBlock(PendingExecution pending, GameLocation location, StardewObject block, Point target, Point stand, List<Point> path,
            int maxMovement, NoteBlockProfile profile, string safeSlotKind, int expectedNextValue, int expectedShake, float expectedScaleY, bool expectedReturn)
        {
            Pending = pending; Location = location; Block = block; Target = target; Stand = stand; Path = path; MaxMovementTiles = maxMovement; Profile = profile;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value; SafeSlotKind = safeSlotKind; RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            BeforeValueRaw = block.preservedParentSheetIndex.Value; BeforeItemId = block.ItemId; BeforeQualifiedItemId = block.QualifiedItemId;
            ExpectedNextValue = expectedNextValue; ExpectedShake = expectedShake; ExpectedScaleY = expectedScaleY; ExpectedReturn = expectedReturn;
            SoundCue = profile.ReasonPrefix == "flute_block" ? pending.Request.FluteBlockSoundCue : pending.Request.DrumBlockSoundCue;
            LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
        }
        public PendingExecution Pending { get; } public GameLocation Location { get; } public StardewObject Block { get; } public Point Target { get; } public Point Stand { get; }
        public List<Point> Path { get; } public int MaxMovementTiles { get; } public NoteBlockProfile Profile { get; } public int SafeSlotIndex { get; }
        public string SafeSlotKind { get; } public int RestoreSlotIndex { get; } public string BeforeValueRaw { get; } public string BeforeItemId { get; }
        public string BeforeQualifiedItemId { get; } public int ExpectedNextValue { get; } public int ExpectedShake { get; } public float ExpectedScaleY { get; }
        public bool ExpectedReturn { get; } public string SoundCue { get; } public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600; public int ElapsedTicks { get; set; } public int PathIndex { get; set; } public int StuckTicks { get; set; }
        public int MovementTiles { get; set; } public Vector2 LastPosition { get; set; } public Point LastObservedTile { get; set; } public bool NativeHandled { get; set; }
    }
}
