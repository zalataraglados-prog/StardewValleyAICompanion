using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SiteApproachObservationReusesOriginalSelectionWithoutReplacingTheFullResourceHead()
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var observedPath = WriteSiteObservation(f);
        var paths = new[] { f.Source.SourcePath, observedPath, f.Source.LedgerPath, f.Inputs.Plan.RequestPath };
        var original = paths.ToDictionary(path => path, File.ReadAllBytes);
        var scope = OpenSiteObservation(f, observedPath);
        var layout = f.Inputs.Request.LayoutIntents.Single();
        var expected = CropTeacherSiteApproachPlanner.SelectApproach(8, 8,
            layout.Tiles.Select(tile => (tile.TileX, tile.TileY)).ToArray(), SiteObservationTiles());
        Assert.NotNull(scope.Target); Assert.NotNull(expected);
        Assert.Equal((expected!.TileX, expected.TileY), (scope.Target!.TileX, scope.Target.TileY));
        Assert.NotEqual(scope.SourceStateHash, scope.ObservationStateHash);
        Assert.Equal(f.Source.Before.StateHash, scope.SourceStateHash);
        Assert.Equal(f.Source.SourcePath, scope.SourcePath);
        Assert.False(scope.AlreadyAtStand); Assert.False(scope.NativeOutcomeVerified);
        Assert.False(scope.ResourceBoundaryVerified); Assert.False(scope.FormalProductTrainingAuthorized);
        scope.ValidateInputsUnchanged();
        foreach (var path in paths) Assert.Equal(original[path], File.ReadAllBytes(path));
        File.AppendAllText(observedPath, " ");
        Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("save")]
    [InlineData("tick")]
    [InlineData("clock")]
    [InlineData("day")]
    [InlineData("position")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("stale_context")]
    [InlineData("missing_context")]
    [InlineData("wrong_type")]
    [InlineData("unavailable")]
    [InlineData("partial")]
    [InlineData("duplicate")]
    [InlineData("bounds")]
    [InlineData("map")]
    [InlineData("map_id")]
    [InlineData("stand_type")]
    [InlineData("no_stand")]
    [InlineData("ledger")]
    public void SiteApproachObservationRejectsUnboundOrIncompletePlanningInputs(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var path = WriteSiteObservation(f);
        var observed = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(path), JsonOptions)!;
        void Context(Action<JsonNode> mutate) => ChangeState(observed, "current_location", location => mutate(location["site_preparation_context"]!));
        switch (change)
        {
            case "actor": observed.PlayerId.Value = "123456"; break;
            case "save": observed.SaveId.Value += ".other"; break;
            case "tick": observed.GameTick++; break;
            case "clock": ChangeState(observed, "time", time => time["time"]!["value"] = 700); break;
            case "day": ChangeState(observed, "time", time => time["total_days"]!["value"] = 999); break;
            case "position": ChangeState(observed, "player", player => player["tile_x"]!["value"] = 7); break;
            case "version": observed.BridgeVersion += ".other"; break;
            case "hash": observed.StateHash = "changed"; break;
            case "stale_context": Context(field => field["read_at_tick"] = observed.GameTick - 1); break;
            case "missing_context": ChangeState(observed, "current_location", location => location.AsObject().Remove("site_preparation_context")); break;
            case "wrong_type": Context(field => field["value"] = new JsonArray()); break;
            case "unavailable": Context(field => field["status"] = "unavailable"); break;
            case "partial": Context(field => field["value"]!["tiles"]!.AsArray().RemoveAt(0)); break;
            case "duplicate": Context(field => field["value"]!["tiles"]![1] = field["value"]!["tiles"]![0]!.DeepClone()); break;
            case "bounds": Context(field => field["value"]!["bounds"]!["max_x"] = 2); break;
            case "map": Context(field => field["value"]!["map_width"] = 21); break;
            case "map_id": ChangeState(observed, "current_location", location => location["map"]!["value"]!["id"] = "different-map"); break;
            case "stand_type": Context(field => field["value"]!["tiles"]![0]!["map_passable"] = "true"); break;
            case "no_stand": Context(field => { foreach (var tile in field["value"]!["tiles"]!.AsArray()) tile!["map_passable"] = false; }); break;
            case "ledger": f.Source.Ledger.SourceStateHash = "other"; Fixture.Save(f.Source.LedgerPath, f.Source.Ledger); break;
        }
        Fixture.Save(path, observed);
        Assert.Throws<InvalidDataException>(() => OpenSiteObservation(f, path));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Fact]
    public void SiteApproachObservationRecognizesExistingStandWithoutNativeOrResourceCredit()
    {
        var f = SiteObservationFixture(atStand: true); using var host = f.Runtime;
        var scope = OpenSiteObservation(f, WriteSiteObservation(f));
        Assert.Null(scope.Target); Assert.True(scope.AlreadyAtStand);
        Assert.False(scope.NativeOutcomeVerified); Assert.False(scope.ResourceBoundaryVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
    }

    private static OwnedRouteFixture SiteObservationFixture(bool atStand = false) => new(prepareSource: source =>
    {
        ChangeState(source.Before, "player", player =>
        { player["tile_x"]!["value"] = atStand ? 0 : 8; player["tile_y"]!["value"] = atStand ? 1 : 8; });
        ChangeState(source.Before, "current_location", location => location["map"]!["value"]!["id"] = "");
    });

    private static CropTeacherSiteApproachTile[] SiteObservationTiles() => Enumerable.Range(0, 3)
        .SelectMany(y => Enumerable.Range(0, 4).Select(x => new CropTeacherSiteApproachTile(x, y, true, false, true))).ToArray();

    private static string WriteSiteObservation(OwnedRouteFixture f)
    {
        var observed = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Source.Before, JsonOptions), JsonOptions)!;
        ChangeState(observed, "current_location", location => location["site_preparation_context"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", read_at_tick = observed.GameTick, value = new
            {
                schema_version = "farm_site_preparation_context.v2", projection_status = "complete_bounded_current_farm_region",
                location_id = "Farm", is_farm = true, map_width = 20, map_height = 20,
                bounds = new { min_x = 0, min_y = 0, max_x = 3, max_y = 2 }, tile_count = 12,
                tiles = SiteObservationTiles().Select(tile => new { tile_x = tile.TileX, tile_y = tile.TileY,
                    map_passable = tile.MapPassable, native_blocked_without_characters = tile.NativeBlockedWithoutCharacters,
                    watering_refill_approach_standable = tile.NativeApproachStandable }).ToArray()
            }
        }));
        ChangeState(observed, "farm", farm => farm["material_inventory_graph"] = JsonSerializer.SerializeToNode(new
        { status = "unavailable", reason_code = "not_requested_by_snapshot_profile" }));
        var path = Path.Combine(f.Source.RuntimePath, "site-observation.json");
        Fixture.Save(path, observed); return path;
    }

    private static CropTeacherSiteApproachObservation OpenSiteObservation(OwnedRouteFixture f, string path) =>
        CropTeacherSiteApproachObservation.Open(f.Inputs.Plan.RequestPath, f.Source.SourcePath, f.Source.LedgerPath,
            f.Inputs.Plan.CoveredCommitmentIds, Id, "site-approach.observation.fixture", path);
}
