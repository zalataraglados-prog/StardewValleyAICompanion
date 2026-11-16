using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static Task<CropTeacherDayBoundaryStart?> CaptureCropTeacherLoopDayBoundaryStartAsync(
        LiveTrainingOptions options, CropTeacherManagedSeedOwner? owner, CropTeacherRunStep? step,
        JsonObject beforeSnapshot, string beforeStateHash) =>
        // Managed dispatch already owns the original overnight/save proof; do not open a second legacy receipt.
        owner is not null ? Task.FromResult<CropTeacherDayBoundaryStart?>(null) :
        CaptureCropTeacherDayBoundaryStartAsync(options, step, beforeSnapshot, beforeStateHash);
}
