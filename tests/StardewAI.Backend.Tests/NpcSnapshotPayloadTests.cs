using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class NpcSnapshotPayloadTests
{
    [Fact]
    public void NpcSchedulesRemainUnavailableWithoutDefaultValue()
    {
        using var json = JsonDocument.Parse(NpcStateJson());
        var npcs = json.RootElement.GetProperty("npcs");
        var schedules = npcs.GetProperty("schedules");

        Assert.Equal("unavailable", schedules.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, schedules.GetProperty("value").ValueKind);
        Assert.Equal(0, schedules.GetProperty("confidence").GetDouble());
        Assert.Equal("npc_schedules_unavailable_without_complete_read_only_decompile_proof", schedules.GetProperty("reason").GetString());
    }

    [Fact]
    public void NpcPositionsParticipateInStableCanonicalHash()
    {
        var first = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(NpcStateJson())!;
        var second = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(NpcStateJson())!;

        Assert.Equal(SnapshotHash.ComputeStateHash(first), SnapshotHash.ComputeStateHash(second));
    }

    private static string NpcStateJson()
    {
        return $$"""
        {
          "npcs": {
            "positions": {{FieldJson("""
            [
              {
                "name": "Abigail",
                "display_name": "Abigail",
                "location_id": "Town",
                "tile_x": 45,
                "tile_y": 68,
                "facing_direction": 2,
                "visible_on_screen": true,
                "is_villager": true,
                "is_monster": false
              }
            ]
            """, raw: true)}},
            "schedules": {{FieldJson(null, "unavailable", reason: "npc_schedules_unavailable_without_complete_read_only_decompile_proof")}}
          }
        }
        """;
    }

    private static string FieldJson(string? value, string status = "available", bool raw = false, string? reason = null)
    {
        var valueJson = value is null
            ? "null"
            : raw
                ? value
                : JsonSerializer.Serialize(value);
        var statusReason = status == "available" || status == "derived"
            ? string.Empty
            : ",\"reason\":" + JsonSerializer.Serialize(reason ?? "value_unavailable");

        return $$"""
        {
          "value": {{valueJson}},
          "status": "{{status}}",
          "source": { "kind": "{{(status == "available" ? "game_object" : "unavailable")}}", "path": "test" },
          "adapter": "test",
          "read_at_tick": 100,
          "confidence": {{(status == "available" ? "1.0" : "0.0")}}{{statusReason}}
        }
        """;
    }
}
