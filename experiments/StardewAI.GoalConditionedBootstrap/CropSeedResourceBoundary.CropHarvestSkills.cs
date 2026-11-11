using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static (CurrentSkillExperienceDeficit Before, CurrentSkillExperienceDeficit After) VerifyCropHarvestSkillEffects(
        SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        var crop = ReadCropHarvestTarget(before, after, request);
        var skillId = crop["harvest_experience_skill_id"]?.GetValue<string>() ?? "";
        var index = Int(crop, "harvest_experience_skill_index");
        var delta = Int(crop, "harvest_experience_on_success_min");
        Require(crop["harvest_experience_projection_status"]?.GetValue<string>() == "exact_from_decompiled_native_harvest" &&
                crop["harvest_experience_condition"] is System.Text.Json.Nodes.JsonValue condition &&
                condition.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) &&
                delta >= 0 && delta == Int(crop, "harvest_experience_on_success_max"),
            "Crop harvest skill effect lacks its original exact target projection.");
        var prior = Unique(Array(Value(Field(before, "player", "skills_detail")!)["skills"], "crop harvest skills"), "skill_id", skillId);
        var models = VerifySkillExperienceDelta(before, after, skillId, index, Int(prior, "experience"), delta, "Crop harvest");
        VerifyHarvestSkillMirrors(before, after, models.Before, models.After);
        return models;
    }

    private static System.Text.Json.Nodes.JsonObject ReadCropHarvestTarget(
        SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request)
    {
        Require(request.OptionId == "executor.harvest_crop" && request.BeforeStateHash == before.StateHash &&
                request.TargetTileX.HasValue && request.TargetTileY.HasValue && !string.IsNullOrWhiteSpace(request.LocationId) &&
                before.SaveId.Value == after.SaveId.Value && before.PlayerId.Value == after.PlayerId.Value &&
                before.GameVersion == after.GameVersion && after.GameTick > before.GameTick,
            "Crop harvest skill source/request identity changed.");
        Require(Value(Field(before, "player", "location_id")!).GetValue<string>() == request.LocationId &&
                Value(Field(after, "player", "location_id")!).GetValue<string>() == request.LocationId,
            "Crop harvest skill target differs from the original action location.");
        var targets = Array(Value(Field(before, "farm", "crops")!), "crop harvest skill targets").OfType<System.Text.Json.Nodes.JsonObject>()
            .Where(row => row["location_id"]?.GetValue<string>() == request.LocationId &&
                Int(row, "tile_x") == request.TargetTileX && Int(row, "tile_y") == request.TargetTileY).ToArray();
        Require(targets is [var target] && HarvestMirrorBoolean(target, "ready_for_harvest") &&
                !HarvestMirrorBoolean(target, "dead"), "Crop harvest skill target is not a unique ready live crop.");
        return targets[0];
    }
}
