using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

internal static class MoneyCropTeacherCaptureFixture
{
    internal static string Save(MoneyCropTeacherFixture fixture, string path)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var layout = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(fixture.LayoutPath), options)!;
        File.WriteAllText(path, JsonSerializer.Serialize(new PlanningHandoffCaptureManifest
        {
            SchemaVersion = "planning_handoff_capture_manifest.v1", Status = "farm_layout_capture_complete",
            SourceStateHash = fixture.SourceSnapshot.StateHash, SourceSnapshotPath = fixture.SnapshotPath,
            SourceSnapshotSha256 = Hash(fixture.SnapshotPath), MapWidth = 4, MapHeight = 3,
            LayoutChunkSize = 32, LayoutSnapshotCount = 1,
            LayoutSnapshots = new[] { new PlanningHandoffLayoutSnapshotBinding
            {
                Path = fixture.LayoutPath, Sha256 = Hash(fixture.LayoutPath), StateHash = layout.StateHash,
                MinX = 0, MinY = 0, MaxX = 3, MaxY = 2, TileCount = 12
            } },
            PlanningHandoff = JsonSerializer.SerializeToElement(new
            { handoff_kind = "crop_production_teacher", automatic_dispatch_required = true })
        }, options));
        return path;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
