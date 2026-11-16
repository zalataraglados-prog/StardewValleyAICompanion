using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeOutputSnapshotAnchorTests
{
    [Fact]
    public void ReadsOnlyTheExactCurrentSnapshotIdentityWithoutGrantingActionCredit()
    {
        var result = Read(Anchor());
        Assert.Equal(new string('a', 64), result.StateHash);
        Assert.Equal(100, result.GameTick);
        Assert.All(typeof(NativeOutputSnapshotAnchor).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Theory]
    [InlineData("schema_version", "unknown")]
    [InlineData("status", "unavailable")]
    [InlineData("snapshot_profile", "light")]
    [InlineData("game_version", "1.6.14")]
    [InlineData("save_id", "other")]
    [InlineData("player_id", "other")]
    [InlineData("run_id", "other")]
    [InlineData("save_isolation_path", "other")]
    [InlineData("state_hash", "short")]
    [InlineData("state_hash", "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("game_tick", "100")]
    public void RejectsUnsupportedOrMismatchedMetadata(string property, string value)
    {
        var json = Anchor(); json[property] = value;
        Assert.Throws<InvalidDataException>(() => Read(json));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("tick")]
    [InlineData("diagnostics")]
    [InlineData("native")]
    [InlineData("formal")]
    [InlineData("null-hash")]
    public void RequiresCompleteExactFieldsAndExplicitNonAuthorizingFlags(string change)
    {
        var json = Anchor();
        switch (change)
        {
            case "missing": json.Remove("game_tick"); break;
            case "unknown": json["unknown"] = 1; break;
            case "tick": json["game_tick"] = 101; break;
            case "diagnostics": json["diagnostics"] = new JsonArray("incomplete"); break;
            case "native": json["native_outcome_verified"] = true; break;
            case "formal": json["formal_product_training_authorized"] = true; break;
            case "null-hash": json["state_hash"] = null; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(json));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("disabled")]
    [InlineData("mode")]
    [InlineData("option")]
    [InlineData("nonce")]
    [InlineData("queue")]
    [InlineData("before")]
    public void RejectsRequestsWithoutAnAdmittedNativeObservationContext(string change)
    {
        var request = Request();
        switch (change)
        {
            case "scope": request.ActionOriginObservationScopeId = null; break;
            case "disabled": request.ActionOriginSnapshotAnchor = false; break;
            case "mode": request.ExecutionMode = "coop_companion"; break;
            case "option": request.OptionId = "executor.wait_ticks"; break;
            case "nonce": request.RequestNonce = ""; break;
            case "queue": request.QueueId = ""; break;
            case "before": request.BeforeStateHash = ""; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeOutputSnapshotAnchor.Read(Anchor().ToJsonString(), request, 100, "farm", "actor"));
    }

    [Fact]
    public void RejectsDuplicateMalformedOrOversizedApiPayloads()
    {
        var json = Anchor().ToJsonString();
        foreach (var payload in new[] { json[..^1] + ",\"status\":\"available\"}", "[]", "{", new string(' ', 16385) })
            Assert.Throws<InvalidDataException>(() => NativeOutputSnapshotAnchor.Read(payload, Request(), 100, "farm", "actor"));
    }

    [Fact]
    public void LegacyWireDoesNotAcquireSnapshotFieldsByDefault()
    {
        using var request = JsonDocument.Parse(JsonSerializer.Serialize(new TrainingExecutionRequest()));
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(new TrainingExecutionResult()));
        Assert.False(request.RootElement.TryGetProperty("action_origin_snapshot_anchor", out _));
        Assert.False(result.RootElement.TryGetProperty("product_after_state_hash", out _));
        Assert.False(result.RootElement.TryGetProperty("product_after_game_tick", out _));
        var restored = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(Request()))!;
        Assert.True(restored.ActionOriginSnapshotAnchor);
    }

    [Fact]
    public void TillingAnchorRequiresExplicitCompleteContractWithoutExpandingLegacyScope()
    {
        var request = Request(); request.OptionId = request.SourcePlanningOptionId = "executor.till_soil";
        Assert.Throws<InvalidDataException>(() => NativeOutputSnapshotAnchor.Read(Anchor().ToJsonString(), request, 100, "farm", "actor"));
        request.ActionOriginTillingResources = request.ActionOriginToolInputs = request.ActionOriginInventoryPayload =
            request.ActionOriginInventoryTransients = request.ActionOriginInventoryGraph = request.ActionOriginDebrisState = true;
        var anchor = NativeOutputSnapshotAnchor.Read(Anchor().ToJsonString(), request, 100, "farm", "actor");
        Assert.Equal(100, anchor.GameTick); Assert.Equal(new string('a', 64), anchor.StateHash);
    }

    [Theory]
    [InlineData("tools")]
    [InlineData("payload")]
    [InlineData("transients")]
    [InlineData("graph")]
    [InlineData("debris")]
    [InlineData("source")]
    [InlineData("foreign_option")]
    public void TillingAnchorRejectsMissingWitnessTiersOrBorrowedAction(string fault)
    {
        var request = Request(); request.OptionId = request.SourcePlanningOptionId = "executor.till_soil";
        request.ActionOriginTillingResources = request.ActionOriginToolInputs = request.ActionOriginInventoryPayload =
            request.ActionOriginInventoryTransients = request.ActionOriginInventoryGraph = request.ActionOriginDebrisState = true;
        switch (fault)
        {
            case "tools": request.ActionOriginToolInputs = false; break;
            case "payload": request.ActionOriginInventoryPayload = false; break;
            case "transients": request.ActionOriginInventoryTransients = false; break;
            case "graph": request.ActionOriginInventoryGraph = false; break;
            case "debris": request.ActionOriginDebrisState = false; break;
            case "source": request.SourcePlanningOptionId = "executor.move_to_tile"; break;
            case "foreign_option": request.OptionId = "executor.clear_obstacle"; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeOutputSnapshotAnchor.Read(Anchor().ToJsonString(), request, 100, "farm", "actor"));
    }

    private static NativeOutputSnapshotAnchor Read(JsonObject json) => NativeOutputSnapshotAnchor.Read(json.ToJsonString(), Request(), 100, "farm", "actor");
    private static TrainingExecutionRequest Request() => new()
    {
        RunId = "run", QueueId = "queue", QueueItemId = "item", RequestNonce = "nonce", BeforeStateHash = "before",
        SourcePlanningOptionId = "executor.move_to_tile", OptionId = "executor.move_to_tile", ExecutionMode = "training_singleplayer",
        SaveIsolationPath = "isolated-saves", ActionOriginObservationScopeId = "scope", ActionOriginSnapshotAnchor = true
    };
    private static JsonObject Anchor() => new()
    {
        ["schema_version"] = "native_output_snapshot_anchor.v1", ["status"] = "available", ["snapshot_profile"] = "full",
        ["state_hash"] = new string('a', 64), ["game_tick"] = 100, ["game_version"] = "1.6.15", ["save_id"] = "farm",
        ["player_id"] = "actor", ["run_id"] = "run", ["save_isolation_path"] = "isolated-saves", ["diagnostics"] = new JsonArray(),
        ["native_outcome_verified"] = false, ["formal_product_training_authorized"] = false
    };
}
