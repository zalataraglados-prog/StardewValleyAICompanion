using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string DrumBlockContract =
        "GameLocation.checkAction->Object.checkForAction_(O)463->CheckForActionOnDrumBlock->preservedParentSheetIndex_next_tone->Game1.playSound_drumkitN->shakeTimer_200->scaleY_1.3";
    private static readonly NoteBlockProfile DrumBlockProfile = new(
        "drum_block", "tune_drum_block", "tone", "Drum Block", "463", "(O)463", "DrumBlock", DrumBlockContract);

    private void StartDrumBlockTuning(PendingExecution pending)
    {
        var request = pending.Request;
        var generic = ValidateExecutionRequest(request);
        if (generic.Count > 0) { pending.Completion.SetResult(Blocked(request, generic.ToArray())); return; }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue || !request.DrumBlockCurrentTone.HasValue || !request.DrumBlockNextTone.HasValue ||
            !request.DrumBlockToneMin.HasValue || !request.DrumBlockToneMax.HasValue || !request.DrumBlockToneStep.HasValue || !request.DrumBlockToneStateCount.HasValue ||
            !request.DrumBlockExpectedShakeTimer.HasValue || !request.DrumBlockExpectedScaleY.HasValue || !request.DrumBlockExpectedLocationActionReturn.HasValue)
        { pending.Completion.SetResult(DrumBlockBlocked(request, "drum_block_typed_fields_required")); return; }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        { pending.Completion.SetResult(DrumBlockBlocked(request, "drum_block_player_or_menu_not_ready")); return; }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var reasons = ValidateDrumBlockTarget(location, target, stand, request, out var block);
        if (reasons.Length > 0) { pending.Completion.SetResult(DrumBlockBlocked(request, reasons)); return; }
        StartNoteBlockTuning(pending, location, block!, target, stand, DrumBlockProfile, request.DrumBlockSafeSlotKind,
            request.DrumBlockNextTone.Value, request.DrumBlockExpectedShakeTimer.Value, request.DrumBlockExpectedScaleY.Value,
            request.DrumBlockExpectedLocationActionReturn.Value, DrumBlockBlocked);
    }

    private static string[] ValidateDrumBlockTarget(GameLocation location, Point target, Point stand, TrainingExecutionRequest request, out StardewObject? block)
    {
        var reasons = ValidateNoteBlockTarget(location, target, stand, request, DrumBlockProfile, request.DrumBlockSafeSlotKind, out block);
        if (block is not null)
        {
            var liveTone = ComputeDrumBlockNextTone(block.preservedParentSheetIndex.Value);
            if (block.preservedParentSheetIndex.Value != request.DrumBlockCurrentToneRaw ||
                request.DrumBlockCurrentTone != liveTone.Current || request.DrumBlockNextTone != liveTone.Next ||
                request.DrumBlockToneMin != 0 || request.DrumBlockToneMax != 6 || request.DrumBlockToneStep != 1 ||
                request.DrumBlockToneStateCount != 7 || request.DrumBlockExpectedShakeTimer != 200 ||
                Math.Abs(request.DrumBlockExpectedScaleY.GetValueOrDefault() - 1.3f) > 0.0001f || request.DrumBlockExpectedLocationActionReturn != true ||
                request.DrumBlockSoundCue != "drumkit" + liveTone.Next) reasons.Add("drum_block_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static (int Current, int Next) ComputeDrumBlockNextTone(string? rawTone)
    {
        _ = int.TryParse(rawTone, out var current);
        return (current, (current + 1) % 7);
    }

    private static TrainingExecutionResult DrumBlockBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "tune_drum_block", $"tone={request.DrumBlockNextTone};sound={request.DrumBlockSoundCue};shake_timer=200;scale_y=1.3", "drum_block_current_state=unverified", reasons);
}
