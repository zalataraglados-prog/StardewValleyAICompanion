using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class ActionOriginOutputSourceGuardTests
{
    [Fact]
    public void DeferredPickupRouteIdentityRoundTrips()
    {
        var request = new TrainingExecutionRequest
        {
            QueueId = "queue.action-origin",
            QueueItemId = "queue-item.pickup",
            AcquisitionRouteKind = "native_location_artifact_spot",
            AcquisitionRouteOccurrenceId = "route-occurrence.330.1",
            DeferredPickupSourceKind = "native_location_artifact_spot",
            DeferredPickupMinimumQuality = 2
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(request, options);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json,
            options)!;

        Assert.Equal(request.AcquisitionRouteKind,
            roundTrip.AcquisitionRouteKind);
        Assert.Equal(request.AcquisitionRouteOccurrenceId,
            roundTrip.AcquisitionRouteOccurrenceId);
        Assert.Equal(2, roundTrip.DeferredPickupMinimumQuality);
    }

    [Fact]
    public void LiveRequestTransportPreservesActionOriginIdentityAndQuality()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecutionRequest.cs");

        Assert.Contains("\"acquisition_route_kind\"", source);
        Assert.Contains("\"acquisition_route_occurrence_id\"", source);
        Assert.Contains("executionRequest.AcquisitionRouteKind", source);
        Assert.Contains("executionRequest.AcquisitionRouteOccurrenceId", source);
        Assert.Contains("executionRequest.DeferredPickupMinimumQuality", source);
    }

    [Fact]
    public void RuntimePickupUsesExactProducedDebrisAndChunkIdentities()
    {
        var attribution = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ActionOriginOutputs.cs");
        var pickup = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");

        Assert.Contains("CaptureDebrisIdentitySet(location)", attribution);
        Assert.Contains(
            "preexistingChunkItems.Keys.ToHashSet(",
            attribution);
        Assert.Contains("!capture.DebrisBefore.Contains(debris)", attribution);
        Assert.Contains("!capture.ChunksBefore.Contains(chunk)", attribution);
        Assert.Contains("CaptureDebrisChunkItems(location)", attribution);
        Assert.Contains("preexistingCollectionAmbiguous", attribution);
        Assert.Contains(
            "deferred_pickup_action_origin_preexisting_collection_ambiguous",
            attribution);
        Assert.Contains("record.ProducedDebris.Contains(debris) ||", attribution);
        Assert.Contains("debris.Chunks.Any(record.ProducedChunks.Contains)",
            attribution);
        Assert.Contains("InventoryBefore", attribution);
        Assert.Contains("MaximumActionOriginQueueRecords", attribution);
        Assert.Contains("TryResolveActionOriginOutput(", pickup);
        Assert.Contains("actionOrigin.ProducedChunks.Contains(value.Chunk)",
            pickup);
        Assert.Contains("ConsumeActionOriginOutput(request)", pickup);
    }

    [Fact]
    public void BushAndGingerReuseChunkLevelActionOriginDifference()
    {
        var state = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.State.Foraging.cs");
        var bushes = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Bushes.cs");
        var ginger = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Ginger.cs");

        Assert.Contains("ChunksBefore = CaptureDebrisChunkIdentitySet(location)",
            state);
        Assert.Contains("active.ChunksBefore", bushes);
        Assert.Contains("CaptureDebrisChunkIdentitySet(location)", ginger);
        Assert.Contains("tool.ChunksBefore!", ginger);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
