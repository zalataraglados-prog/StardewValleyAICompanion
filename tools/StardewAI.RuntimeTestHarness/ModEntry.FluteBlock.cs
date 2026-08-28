using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FluteBlockContract =
        "GameLocation.checkAction->Object.checkForAction_(O)464->CheckForActionOnFluteBlock->preservedParentSheetIndex_next_pitch->Game1.playSound_flute_pitch->shakeTimer_200->scaleY_1.3";
    private static readonly NoteBlockProfile FluteBlockProfile = new(
        "flute_block", "tune_flute_block", "pitch", "Flute Block", "464", "(O)464", "FluteBlock", FluteBlockContract);

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
        StartNoteBlockTuning(pending, location, block!, target, stand, FluteBlockProfile, request.FluteBlockSafeSlotKind,
            request.FluteBlockNextPitch.Value, request.FluteBlockExpectedShakeTimer.Value, request.FluteBlockExpectedScaleY.Value,
            request.FluteBlockExpectedLocationActionReturn.Value, FluteBlockBlocked);
    }

    private static string[] ValidateFluteBlockTarget(GameLocation location, Point target, Point stand, TrainingExecutionRequest request, out StardewObject? block)
    {
        var reasons = ValidateNoteBlockTarget(location, target, stand, request, FluteBlockProfile, request.FluteBlockSafeSlotKind, out block);
        if (block is not null)
        {
            var livePitch = ComputeFluteBlockNextPitch(block.preservedParentSheetIndex.Value);
            if (block.preservedParentSheetIndex.Value != request.FluteBlockCurrentPitchRaw ||
                request.FluteBlockCurrentPitch != livePitch.Current || request.FluteBlockNextPitch != livePitch.Next ||
                request.FluteBlockPitchMin != 0 || request.FluteBlockPitchMax != 2400 || request.FluteBlockPitchStep != 100 ||
                request.FluteBlockPitchStateCount != 25 || request.FluteBlockExpectedShakeTimer != 200 ||
                Math.Abs(request.FluteBlockExpectedScaleY.GetValueOrDefault() - 1.3f) > 0.0001f || request.FluteBlockExpectedLocationActionReturn != true ||
                request.FluteBlockSoundCue != "flute") reasons.Add("flute_block_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static (int Current, int Next) ComputeFluteBlockNextPitch(string? rawPitch)
    {
        _ = int.TryParse(rawPitch, out var current);
        var next = current switch { 2300 => 2400, 2400 => 0, _ => (current + 100) % 2400 };
        return (current, next);
    }

    private static TrainingExecutionResult FluteBlockBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "tune_flute_block", $"pitch={request.FluteBlockNextPitch};sound=flute;shake_timer=200;scale_y=1.3", "flute_block_current_state=unverified", reasons);
}
