using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeCropDayGrowthTests
{
    [Theory]
    [InlineData("watered")]
    [InlineData("dry")]
    [InlineData("no-watering-required")]
    [InlineData("phase-boundary")]
    [InlineData("mature")]
    [InlineData("zero-phases")]
    [InlineData("negative-phase")]
    [InlineData("zero-current-phase")]
    [InlineData("last-phase")]
    [InlineData("terminal-clamp")]
    [InlineData("regrow")]
    [InlineData("regrow-ready")]
    [InlineData("dry-regrow")]
    [InlineData("no-watering-regrow")]
    public void NativeCropDayGrowthUsesOriginalProjectionWithoutMutatingInputs(string scenario)
    {
        var (before, after) = Fixture(scenario);
        var original = JsonSerializer.Serialize(new[] { before, after });
        Verify(before, after);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { before, after }));
    }

    [Theory]
    [InlineData("missing-before")]
    [InlineData("missing-after")]
    [InlineData("dead-before")]
    [InlineData("dead-after")]
    [InlineData("seed-before")]
    [InlineData("seed-after")]
    [InlineData("raw-seed-drift")]
    [InlineData("harvest-before")]
    [InlineData("harvest-after")]
    [InlineData("phase-days")]
    [InlineData("phase")]
    [InlineData("phase-count")]
    [InlineData("phase-day")]
    [InlineData("fully-grown")]
    [InlineData("ready")]
    [InlineData("remaining")]
    [InlineData("regrow-days")]
    [InlineData("missing-catalog")]
    [InlineData("duplicate-catalog")]
    [InlineData("wrong-catalog-seed")]
    [InlineData("missing-watering-rule")]
    [InlineData("duplicate-tile")]
    public void NativeCropDayGrowthRejectsMissingIdentityOrBadProjection(string change)
    {
        var (before, after) = Fixture("watered");
        switch (change)
        {
            case "missing-before": Edit(before, "crops", rows => rows.AsArray().Clear()); break;
            case "missing-after": Edit(after, "crops", rows => rows.AsArray().Clear()); break;
            case "dead-before": ChangeCrop(before, "dead", JsonValue.Create(true)); break;
            case "dead-after": ChangeCrop(after, "dead", JsonValue.Create(true)); break;
            case "seed-before": ChangeCrop(before, "harvest_source_seed_id", JsonValue.Create("475")); break;
            case "seed-after": ChangeCrop(after, "harvest_source_seed_id", JsonValue.Create("475")); break;
            case "raw-seed-drift": ChangeCrop(after, "harvest_source_seed_id", JsonValue.Create("(O)474")); break;
            case "harvest-before": ChangeCrop(before, "harvest_item_qualified_id", JsonValue.Create("(O)191")); break;
            case "harvest-after": ChangeCrop(after, "harvest_item_qualified_id", JsonValue.Create("(O)191")); break;
            case "phase-days": ChangeCrop(after, "phase_days", new JsonArray(2, 4, 99999)); break;
            case "phase": ChangeCrop(after, "current_phase", JsonValue.Create(1)); break;
            case "phase-count": ChangeCrop(after, "phase_count", JsonValue.Create(4)); break;
            case "phase-day": ChangeCrop(after, "day_of_current_phase", JsonValue.Create(0)); break;
            case "fully-grown": ChangeCrop(after, "fully_grown", JsonValue.Create(true)); break;
            case "ready": ChangeCrop(after, "ready_for_harvest", JsonValue.Create(true)); break;
            case "remaining": ChangeCrop(after, "days_until_next_harvest_if_watered", JsonValue.Create(5)); break;
            case "regrow-days": ChangeCrop(after, "regrow_days", JsonValue.Create(4)); break;
            case "missing-catalog": Edit(before, "crop_catalog", rows => rows.AsArray().Clear()); break;
            case "duplicate-catalog": Edit(before, "crop_catalog", rows => rows.AsArray().Add(rows[0]!.DeepClone())); break;
            case "wrong-catalog-seed": Edit(before, "crop_catalog", rows => rows[0]!["seed_id"] = "475"); break;
            case "missing-watering-rule": Edit(before, "crop_catalog", rows => rows[0]!.AsObject().Remove("needs_watering")); break;
            case "duplicate-tile": Edit(after, "crops", rows => rows.AsArray().Add(rows[0]!.DeepClone())); break;
        }
        var original = JsonSerializer.Serialize(new[] { before, after });
        Assert.Throws<InvalidDataException>(() => Verify(before, after));
        Assert.Equal(original, JsonSerializer.Serialize(new[] { before, after }));
    }

    [Theory]
    [InlineData("dry", true)]
    [InlineData("watered", false)]
    public void NativeCropDayGrowthDoesNotConfuseWateredGrowthWithDryNoGrowth(string scenario, bool unexpectedGrowth)
    {
        var (before, after) = Fixture(scenario);
        ChangeCrop(after, "day_of_current_phase", JsonValue.Create(unexpectedGrowth ? 1 : 0));
        ChangeCrop(after, "days_until_next_harvest_if_watered", JsonValue.Create(unexpectedGrowth ? 4 : 5));
        var error = Assert.Throws<InvalidDataException>(() => Verify(before, after));
        Assert.Equal("Money Revenue crop state did not follow Crop.newDay on tile 10,11.", error.Message);
    }

    [Fact]
    public void NativeCropDayGrowthReadsSelectedBeforeCatalogAndDoesNotBecomeAWorldOrLifecycleGate()
    {
        var (before, after) = Fixture("no-watering-required");
        // Only the selected BEFORE catalog governs growth. Other world/crop state is caller-owned.
        Edit(before, "crop_catalog", rows => rows.AsArray().Insert(0,
            new JsonObject { ["seed_id"] = "475", ["needs_watering"] = true }));
        Edit(after, "crop_catalog", rows => rows.AsArray().Clear());
        Edit(after, "crops", rows => rows.AsArray().Add(new JsonObject
            { ["location_id"] = "Farm", ["tile_x"] = 20, ["tile_y"] = 11 }));
        ChangeCrop(before, "harvest_source_seed_id", JsonValue.Create("(O)474"));
        ChangeCrop(after, "harvest_source_seed_id", JsonValue.Create("(O)474"));
        Verify(before, after);
        Assert.Single(before.State); // These declared fixtures have no calendar, queue, receipt or save proof.
    }

    [Fact]
    public void NativeCropDayGrowthKeepsOriginalFarmOnlyReaderBoundary()
    {
        var (before, after) = Fixture("watered");
        var error = Assert.Throws<InvalidDataException>(() => Verify(before, after, "Town"));
        Assert.Equal("Money Revenue native-lifecycle v1 supports the authoritative Farm crop projection only.", error.Message);
    }

    private static void Verify(SnapshotEnvelope before, SnapshotEnvelope after, string location = "Farm")
    {
        var method = typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("VerifyOwnedDayGrowth",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        try { method.Invoke(null, [before, after, location, new MoneyRevenueCropPlantingTile(10, 11), "(O)474", "(O)190"]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    // Declared isolated projection fixtures, NOT captured native frames or a second growth calculator.
    // Each tuple spells out its before/after expectations independently of ProjectNativeDay.
    private static (SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(string scenario)
    {
        var phases = new[] { 2, 3, 99999 };
        var watered = true;
        var needsWatering = true;
        var fullyGrown = false;
        int? regrow = null;
        var prior = (Phase: 0, Day: 0, Ready: false, Remaining: 5);
        var next = (Phase: 0, Day: 1, Ready: false, Remaining: 4);
        switch (scenario)
        {
            case "watered": break;
            case "dry": watered = false; next = prior; break;
            case "no-watering-required": watered = false; needsWatering = false; break;
            case "phase-boundary": prior = (0, 1, false, 4); next = (1, 0, false, 3); break;
            case "mature": prior = (1, 2, false, 1); next = (2, 0, true, 0); break;
            case "zero-phases": phases = [1, 0, 0, 2, 99999]; prior = (0, 0, false, 3); next = (3, 0, false, 2); break;
            case "negative-phase": phases = [1, -1, 0, 2, 99999]; prior = (0, 0, false, 3); next = (3, 0, false, 2); break;
            case "zero-current-phase": phases = [0, 2, 99999]; prior = (0, 0, false, 2); next = (1, 0, false, 2); break;
            case "last-phase": prior = (2, 0, true, 0); next = (2, 1, true, 0); break;
            case "terminal-clamp": prior = (2, 99999, true, 0); next = prior; break;
            case "regrow": fullyGrown = true; regrow = 4; prior = (2, 2, false, 2); next = (2, 1, false, 1); break;
            case "regrow-ready": fullyGrown = true; regrow = 4; prior = (2, 1, false, 1); next = (2, 0, true, 0); break;
            case "dry-regrow": fullyGrown = true; regrow = 4; watered = false; prior = (2, 2, false, 2); next = prior; break;
            case "no-watering-regrow": fullyGrown = true; regrow = 4; watered = false; needsWatering = false;
                prior = (2, 2, false, 2); next = (2, 1, false, 1); break;
            default: throw new ArgumentException("Unknown declared fixture: " + scenario);
        }
        JsonObject Crop((int Phase, int Day, bool Ready, int Remaining) state) => new()
        {
            ["location_id"] = "Farm", ["tile_x"] = 10, ["tile_y"] = 11,
            ["harvest_source_seed_id"] = "474", ["harvest_item_qualified_id"] = "(O)190",
            ["phase_days"] = JsonSerializer.SerializeToNode(phases), ["phase_count"] = phases.Length,
            ["current_phase"] = state.Phase, ["day_of_current_phase"] = state.Day, ["dead"] = false,
            ["fully_grown"] = fullyGrown, ["ready_for_harvest"] = state.Ready,
            ["days_until_next_harvest_if_watered"] = state.Remaining, ["regrow_days"] = regrow,
            ["watered"] = watered
        };
        SnapshotEnvelope Snapshot(JsonObject crop) => new()
        {
            State = new Dictionary<string, JsonElement>
            {
                ["farm"] = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["crops"] = new JsonObject { ["status"] = "available", ["value"] = new JsonArray(crop) },
                    ["crop_catalog"] = new JsonObject { ["status"] = "available", ["value"] = new JsonArray(
                        new JsonObject { ["seed_id"] = "474", ["needs_watering"] = needsWatering, ["regrow_days"] = regrow }) }
                })
            }
        };
        var before = Snapshot(Crop(prior));
        var after = Snapshot(Crop(next));
        // Native day-state equality intentionally excludes watered; overnight soil settlement is separate.
        ChangeCrop(after, "watered", JsonValue.Create(false));
        before.StateHash = SnapshotHash.ComputeStateHash(before.State);
        return (before, after);
    }

    private static void ChangeCrop(SnapshotEnvelope snapshot, string name, JsonNode? value) =>
        Edit(snapshot, "crops", rows => rows[0]![name] = value);

    private static void Edit(SnapshotEnvelope snapshot, string field, Action<JsonNode> edit)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        edit(farm[field]!["value"]!);
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
