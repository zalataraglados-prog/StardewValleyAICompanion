using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class MiningResourceClumpClosureTests
{
    [Fact]
    public void CompilerRebindsCompleteLiveResourceClumpProjection()
    {
        var snapshot = ResourceClumpSnapshot();
        var request = ResourceClumpRequest(snapshot.StateHash);

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CompilerBlocksResourceClumpHealthDrift()
    {
        var snapshot = ResourceClumpSnapshot();
        var request = ResourceClumpRequest(snapshot.StateHash);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Select(parameter => parameter.Name == "resource_clump_health"
                ? new SmallModelActionParameter
                {
                    Name = parameter.Name,
                    Value = "19"
                }
                : parameter)
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(
            request,
            snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "mining_resource_clump_tool_or_health_projection_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeSmokeCoversEverySupportedVanillaMiningIndex()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeMiningResourceClumpSmoke.ps1");

        foreach (var index in new[]
        {
            148, 622, 672, 752, 754, 756, 758
        })
        {
            Assert.Contains(
                "parent_sheet_index = " + index,
                source,
                StringComparison.Ordinal);
        }
        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"JunimoTestClient\"",
            source,
            StringComparison.Ordinal);
    }

    private static SnapshotEnvelope ResourceClumpSnapshot()
    {
        const string outputJson =
            "[{\\\"RuntimeType\\\":\\\"StardewValley.Object\\\"," +
            "\\\"QualifiedItemId\\\":\\\"(O)390\\\"," +
            "\\\"Quality\\\":0,\\\"UnitStateSha256\\\":\\\"hash\\\"," +
            "\\\"Quantity\\\":10}]";
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"UndergroundMine5","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(T)GoldPickaxe"}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus":{
                "active_menu":{"value":null,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "mining":{
                "current_mine":{"value":{"location_id":"UndergroundMine5","mine_level":5,"mine_kind":"ordinary_mines"},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tiles":{"value":{"map":{"width":20,"height":20},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":20,"height":20,"blocked_rows":[]}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "resource_clumps":{"value":[{"tile_x":10,"tile_y":7,"width":2,"height":2,"parent_sheet_index":148,"runtime_type":"StardewValley.TerrainFeatures.ResourceClump","health":20,"required_tool":"pickaxe","minimum_upgrade_level":3,"selected_tool_slot_index":2,"selected_tool_qualified_item_id":"(T)GoldPickaxe","selected_tool_upgrade_level":3,"selected_tool_additional_power":0,"selected_tool_effective_upgrade_level":3,"damage_per_hit":3,"expected_hits_remaining":7,"expected_core_output_items_json":"OUTPUT_JSON","possible_secret_note_qualified_item_id":"(O)79","executor_status":"native_executor_available"}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "monsters":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "player_resources":{"value":{"health":100,"energy":200},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """.Replace("OUTPUT_JSON", outputJson));
    }

    private static SmallModelActionEnvelope ResourceClumpRequest(
        string stateHash)
    {
        const string outputJson =
            "[{\"RuntimeType\":\"StardewValley.Object\"," +
            "\"QualifiedItemId\":\"(O)390\",\"Quality\":0," +
            "\"UnitStateSha256\":\"hash\",\"Quantity\":10}]";
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.resource-clump.test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.resource-clump.test",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "action.resource-clump.test",
                    OptionId = "executor.break_resource_clump",
                    Parameters = new[]
                    {
                        P("target_tile_x", "10"),
                        P("target_tile_y", "7"),
                        P("stand_tile_x", "10"),
                        P("stand_tile_y", "6"),
                        P("resource_clump_tile_x", "10"),
                        P("resource_clump_tile_y", "7"),
                        P("resource_clump_width", "2"),
                        P("resource_clump_height", "2"),
                        P("resource_clump_parent_sheet_index", "148"),
                        P("resource_clump_health", "20"),
                        P("resource_clump_minimum_upgrade_level", "3"),
                        P(
                            "resource_clump_tool_qualified_item_id",
                            "(T)GoldPickaxe"),
                        P("resource_clump_tool_upgrade_level", "3"),
                        P("resource_clump_tool_additional_power", "0"),
                        P(
                            "resource_clump_tool_effective_upgrade_level",
                            "3"),
                        P("resource_clump_damage_per_hit", "3"),
                        P("tool_slot_index", "2"),
                        P("required_tool_kind", "pickaxe"),
                        P("max_tool_swings", "9"),
                        P("target_runtime_type",
                            "StardewValley.TerrainFeatures.ResourceClump"),
                        P("expected_output_items_json", outputJson),
                        P(
                            "possible_secret_note_qualified_item_id",
                            "(O)79")
                    }
                }
            }
        };
    }

    private static SmallModelActionParameter P(
        string name,
        string value)
    {
        return new SmallModelActionParameter
        {
            Name = name,
            Value = value
        };
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-29T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string ReadRepositoryFile(
        params string[] segments)
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
            directory?.FullName ??
                throw new InvalidOperationException(
                    "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
