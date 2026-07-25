using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    private const string MachineRemovalNativeContract =
        "Pickaxe.DoFunction_to_Object.performToolAction_then_performRemoveAction_and_exact_machine_debris";

    [Fact]
    public void CompileRemoveMachineRequiresExplicitIntentAndSafeProjection()
    {
        var snapshot = MachineRemovalSnapshot(safe: true);
        var request = Request(
            snapshot.StateHash,
            "executor.remove_machine");
        request.Actions[0].Parameters =
            MachineRemovalParameters(includeIntent: true);

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(
                ";",
                queue.Items.SelectMany(
                    row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("remove_machine", step.StepType);
        Assert.Contains(
            "intent=layout:test",
            step.Target,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine_recovery[(BC)13]=debris_or_native_auto_collected_inventory",
            step.ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompileRemoveMachineBlocksProcessingOrAttachedProjection()
    {
        var snapshot = MachineRemovalSnapshot(safe: false);
        var request = Request(
            snapshot.StateHash,
            "executor.remove_machine");
        request.Actions[0].Parameters =
            MachineRemovalParameters(includeIntent: true);

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "remove_machine_safety_projection_blocked",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileRemoveMachineBlocksWithoutRelocationIntent()
    {
        var snapshot = MachineRemovalSnapshot(safe: true);
        var request = Request(
            snapshot.StateHash,
            "executor.remove_machine");
        request.Actions[0].Parameters =
            MachineRemovalParameters(includeIntent: false);

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "remove_machine_typed_target_and_intent_fields_required",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelActionParameter[]
        MachineRemovalParameters(bool includeIntent)
    {
        var parameters = new List<SmallModelActionParameter>
        {
            new() { Name = "target_location", Value = "Farm" },
            new() { Name = "location_id", Value = "Farm" },
            new() { Name = "target_tile_x", Value = "60" },
            new() { Name = "target_tile_y", Value = "15" },
            new() { Name = "stand_tile_x", Value = "61" },
            new() { Name = "stand_tile_y", Value = "15" },
            new() { Name = "qualified_item_id", Value = "(BC)13" },
            new() { Name = "tool_slot_index", Value = "1" },
            new()
            {
                Name = "tool_qualified_item_id",
                Value = "(T)Pickaxe"
            },
            new()
            {
                Name = "native_contract",
                Value = MachineRemovalNativeContract
            },
            new()
            {
                Name = "machine_removal_projection_fingerprint",
                Value = "fingerprint:test"
            }
        };
        if (includeIntent)
        {
            parameters.Add(
                new SmallModelActionParameter
                {
                    Name = "relocation_intent_id",
                    Value = "layout:test"
                });
        }
        return parameters.ToArray();
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope
        MachineRemovalSnapshot(bool safe)
    {
        return Snapshot(
            """
            {
              "player": {
                "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory": {"value":[{"slot_index":1,"qualified_item_id":"(T)Pickaxe","stack":1,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus": {
                "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "locations": {
                "collision_grid": {"value":{"width":120,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "farm": {
                "machines": {"value":[{"location_id":"Farm","location_is_current":true,"tile_x":60,"tile_y":15,"qualified_item_id":"(BC)13","runtime_type":"StardewValley.Object","object_type":"Crafting","fragility":0,"ready_for_harvest":false,"minutes_until_ready":MINUTES,"held_item":HELD_ITEM,"removal_status":"REMOVAL_STATUS","removal_safe_now":SAFE,"removal_block_reasons":BLOCK_REASONS,"removal_tool_slot_index":1,"removal_tool_qualified_item_id":"(T)Pickaxe","removal_native_contract":"NATIVE_CONTRACT","removal_projection_fingerprint":"fingerprint:test"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """
            .Replace("MINUTES", safe ? "-1" : "20")
            .Replace("HELD_ITEM", safe ? "null" : """{"qualified_item_id":"(O)334"}""")
            .Replace(
                "REMOVAL_STATUS",
                safe ? "safe_idle_native_pickaxe" : "blocked")
            .Replace("SAFE", safe ? "true" : "false")
            .Replace(
                "BLOCK_REASONS",
                safe
                    ? "[]"
                    : """["machine_removal_processing","machine_removal_held_item_or_attachment_present"]""")
            .Replace(
                "NATIVE_CONTRACT",
                MachineRemovalNativeContract));
    }
}
