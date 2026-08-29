using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyTreasureTotemRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_treasure_totem", StringComparison.Ordinal))
            return;
        request.TreasureTotemProjectionFingerprint = ReadQueueParameterString(item, "treasure_totem_projection_fingerprint");
        request.TreasureTotemCenterTileX = ReadQueueParameterInt(item, "center_tile_x");
        request.TreasureTotemCenterTileY = ReadQueueParameterInt(item, "center_tile_y");
        request.TreasureTotemRingCandidateCount = ReadQueueParameterInt(item, "ring_candidate_count");
        request.TreasureTotemExpectedSpawnCount = ReadQueueParameterInt(item, "expected_spawn_count");
        request.TreasureTotemExpectedSpawnTilesJson = ReadQueueParameterString(item, "expected_spawn_tiles_json");
        request.TreasureTotemExistingArtifactSpotCountBefore = ReadQueueParameterInt(item, "existing_artifact_spot_count_before");
        request.TreasureTotemExistingArtifactSpotCountAfter = ReadQueueParameterInt(item, "existing_artifact_spot_count_after");
        request.TreasureTotemsUsedBefore = ReadQueueParameterInt(item, "treasure_totems_used_before");
        request.TreasureTotemsUsedAfter = ReadQueueParameterInt(item, "treasure_totems_used_after");
        request.TreasureTotemRingScanRadius = ReadQueueParameterInt(item, "native_ring_scan_radius");
        request.TreasureTotemRoundedRadius = ReadQueueParameterInt(item, "native_rounded_radius");
        request.TreasureTotemArtifactSpotQualifiedItemId = ReadQueueParameterString(item, "artifact_spot_qualified_item_id");
        request.TreasureTotemInitialSound = ReadQueueParameterString(item, "native_initial_sound");
    }
}
