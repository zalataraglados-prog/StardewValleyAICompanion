using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private bool OriginalCropBatchMatches(CropTeacherRunStep step, string optionId)
    {
        var batch = step.ActionBatch;
        var original = plan.Days.SingleOrDefault(day => day.TotalDay == step.TotalDay)?.ActionBatches
            .SingleOrDefault(row => row.BatchIndex == batch?.BatchIndex);
        return step.Stage == CropTeacherRunStageKinds.ExecuteCropBatch && batch is not null && batch.OptionId == optionId &&
            batch.CommitmentId == step.CommitmentId && plan.CoveredCommitmentIds.Contains(step.CommitmentId, StringComparer.Ordinal) && original is not null &&
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(batch with { Tiles = original.Tiles }, JsonOptions), JsonSerializer.SerializeToNode(original, JsonOptions)) &&
            batch.Tiles.Length is >= 1 and <= 8 && batch.Tiles.SequenceEqual(original.Tiles.Where(tile => batch.Tiles.Contains(tile)));
    }
}
