using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompilerRejectsOrdinaryIncubatorCollection()
    {
        var snapshot = AddIncubatorMarker(
            MachineOutputSnapshot(inventoryHasEmptySlot: true));
        var request = Request(
            snapshot.StateHash,
            "executor.collect_machine_output");
        request.Actions[0].Parameters = new[]
        {
            Parameter("target_tile_x", "64"),
            Parameter("target_tile_y", "15"),
            Parameter("target_location", "Farm"),
            Parameter("machine_location_id", "Farm"),
            Parameter("qualified_item_id", "(O)388"),
            Parameter("machine_harvest_experience_raw", ""),
            Parameter(
                "expected_skill_experience_deltas_json",
                "[]"),
            Parameter("expected_mastery_experience_delta", "0"),
            Parameter(
                "skill_experience_projection_status",
                "exact_no_configured_experience"),
            Parameter(
                "skill_experience_condition",
                "native_machine_output_collection")
        };

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "collect_machine_output_requires_incubator_hatch_flow",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompilerRejectsOrdinaryIncubatorLoading()
    {
        var snapshot = AddIncubatorMarker(
            MachineInputSnapshot(includeInputProbe: true));
        var request = Request(
            snapshot.StateHash,
            "executor.load_machine_input");
        request.Actions[0].Parameters = new[]
        {
            Parameter("target_tile_x", "64"),
            Parameter("target_tile_y", "15"),
            Parameter("target_location", "Farm"),
            Parameter("machine_location_id", "Farm"),
            Parameter("input_slot_index", "0"),
            Parameter("qualified_item_id", "(O)262")
        };

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "load_machine_input_requires_incubator_hatch_flow",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value)
    {
        return new SmallModelActionParameter
        {
            Name = name,
            Value = value
        };
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope
        AddIncubatorMarker(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot.State);
        json = json.Replace(
            "\"ready_for_harvest\":",
            "\"machine_is_incubator\":true," +
            "\"machine_data\":{\"is_incubator\":true}," +
            "\"ready_for_harvest\":",
            StringComparison.Ordinal);
        return Snapshot(json);
    }
}
