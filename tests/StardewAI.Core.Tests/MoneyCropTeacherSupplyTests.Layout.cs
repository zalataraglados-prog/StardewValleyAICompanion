using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using Fixture = StardewAI.Testing.MoneyCropTeacherFixture;

namespace StardewAI.Core.Tests;

public sealed partial class MoneyCropTeacherSupplyTests
{
    [Fact]
    public void OversizedLegacyChoiceUsesOneCapturedBoundedRegion()
    {
        using var fixture = new Fixture();
        fixture.SetLayouts(WideLayout(0, 31), WideLayout(32, 39));
        var decision = fixture.Build();
        var commitment = Assert.Single(decision.Commitments);
        Assert.Equal(5, commitment.TileCount);
        Assert.All(commitment.Tiles, tile => Assert.InRange(tile.TileX, 0, 31));
        Assert.True(fixture.Receipt(decision).AllCommitmentsAccepted);
    }

    [Fact]
    public void ReversingLayoutInputsPreservesTheBoundedChoice()
    {
        using var fixture = new Fixture();
        fixture.SetLayouts(WideLayout(0, 31), WideLayout(32, 39));
        var first = fixture.Build();
        fixture.SetLayouts(WideLayout(32, 39), WideLayout(0, 31));
        var second = fixture.Build();
        Assert.Equal(first.Commitments[0].Tiles, second.Commitments[0].Tiles);
        Assert.Equal(JsonSerializer.Serialize(first.TeacherCandidateEvaluations, JsonOptions),
            JsonSerializer.Serialize(second.TeacherCandidateEvaluations, JsonOptions));
    }

    [Fact]
    public void MissingBoundedCapacityRejectsBeforeEmission()
    {
        using var fixture = new Fixture();
        fixture.SetLayouts(WideLayout(0, 31, sparse: true),
            WideLayout(32, 39, sparse: true));
        Assert.Contains("no single bounded Farm region",
            Assert.Throws<InvalidDataException>(() => fixture.Build()).Message);
    }

    private static SnapshotEnvelope WideLayout(int minX, int maxX, bool sparse = false)
    {
        var snapshot = OriginalFixture("MoneyCropTeacherLayoutSnapshot",
            40, true, "476", 1, false, false, false);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var context = farm["site_preparation_context"]!["value"]!;
        context["map_width"] = 40;
        context["map_height"] = 3;
        context["bounds"] = new JsonObject
        {
            ["min_x"] = minX, ["min_y"] = 0,
            ["max_x"] = maxX, ["max_y"] = 2
        };
        context["tile_count"] = (maxX - minX + 1) * 3;
        var tiles = new JsonArray();
        for (var y = 0; y < 3; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var viable = y == 0 && (sparse ? x is 0 or 1 or 2 or 38 or 39 : x <= 8 || x == 39);
            var water = x == 38 && y == 2;
            tiles.Add(new JsonObject
            {
                ["tile_x"] = x, ["tile_y"] = y,
                ["status"] = viable ? x == 39 ? "already_prepared" : "ready_to_till" : "blocked_not_diggable",
                ["map_passable"] = !water,
                ["native_blocked_without_characters"] = water,
                ["native_walking_collision_blocked"] = water,
                ["watering_can_refill_source"] = water,
                ["watering_refill_approach_standable"] = !water,
                ["clearable_blocker_kinds"] = new JsonArray(),
                ["resource_clump_anchors"] = new JsonArray()
            });
        }
        context["tiles"] = tiles;
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
