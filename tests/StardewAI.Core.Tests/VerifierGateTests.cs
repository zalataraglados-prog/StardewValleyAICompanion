using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.PreviewCompiler;
using StardewAI.Core.Verifier;

namespace StardewAI.Core.Tests;

public sealed class VerifierGateTests
{
    [Fact]
    public void DerivedFactPolicyTests()
    {
        var option = Registry("executor.wait_ticks");
        option.RequiredFactPolicy.FactOverrides = new[]
        {
            new RequiredFactRule
            {
                StateFactor = "time.time",
                AllowedStatuses = new[] { "available", "derived" },
                MinimumConfidence = 1,
                MaximumAgeTicks = 120,
                RequiredProvenanceKinds = new[] { "game_object" },
                AllowedAdapterIds = new[] { "test" },
                AllowedDerivationIds = new[] { "test.clock" }
            }
        };
        var snapshot = Snapshot(
            100,
            Field("time", "time", "600", status: "derived", derivation: "test.clock"));

        var result = new StardewAI.Core.Verifier.Verifier().Verify(snapshot, option);

        Assert.True(result.ReadEligible);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void DerivedFactWithoutAuthorizedDerivationFailsClosed()
    {
        var option = Registry("executor.wait_ticks");
        option.RequiredFactPolicy.DefaultRule.AllowedStatuses = new[] { "available", "derived" };
        option.RequiredFactPolicy.DefaultRule.AllowedDerivationIds = new[] { "test.allowed" };
        var snapshot = Snapshot(
            100,
            Field("time", "time", "600", status: "derived", derivation: "test.other"));

        var result = new StardewAI.Core.Verifier.Verifier().Verify(snapshot, option);

        Assert.False(result.ReadEligible);
        Assert.Contains("required_fact_derivation_denied", result.BlockingReasons);
    }

    [Fact]
    public void RequiredFactFreshnessTests()
    {
        var result = new StardewAI.Core.Verifier.Verifier().Verify(
            Snapshot(500, Field("time", "time", "600", readAtTick: 100)),
            Registry("executor.wait_ticks"));

        Assert.False(result.ReadEligible);
        Assert.Contains("required_fact_stale", result.BlockingReasons);
    }

    [Fact]
    public void RequiredFactProvenanceTests()
    {
        var result = new StardewAI.Core.Verifier.Verifier().Verify(
            Snapshot(100, Field("time", "time", "600", sourceKind: "model_guess")),
            Registry("executor.wait_ticks"));

        Assert.False(result.ReadEligible);
        Assert.Contains("required_fact_provenance_denied", result.BlockingReasons);
    }

    [Fact]
    public void UnknownAdapterFailsClosedTests()
    {
        var result = new StardewAI.Core.Verifier.Verifier().Verify(
            Snapshot(100, Field("time", "time", "600", adapter: "unknown_mod_adapter")),
            Registry("executor.wait_ticks"));

        Assert.False(result.ReadEligible);
        Assert.Contains("required_fact_adapter_denied", result.BlockingReasons);
    }

    [Fact]
    public void UnboundCandidateNotCompileReadyTests()
    {
        var option = Evaluate(
            Snapshot(100, Field("time", "time", "600")),
            new OptionAvailabilityCandidate { OptionId = "executor.wait_ticks" });

        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.Available);
    }

    [Fact]
    public void BoundCandidateRunsCompilerProbeTests()
    {
        var option = Evaluate(
            Snapshot(100, Field("time", "time", "600")),
            Candidate("executor.wait_ticks", ("wait_ticks", "10")));

        Assert.True(option.ReadEligible);
        Assert.Equal("bound", option.BindingStatus);
        Assert.Equal("ready", option.CompileStatus);
        Assert.Equal("authorized", option.ExecutionAuthorization);
        Assert.True(option.Available);
    }

    [Fact]
    public void ActionSpecificValidateGateStillRunsTests()
    {
        var option = Evaluate(
            Snapshot(100, Field("time", "time", "600")),
            Candidate("executor.wait_ticks", ("wait_ticks", "-1")));

        Assert.Equal("bound", option.BindingStatus);
        Assert.Equal("blocked", option.CompileStatus);
        Assert.Contains("wait_ticks_1_600_required", option.BlockingReasons);
        Assert.False(option.Available);
    }

    [Fact]
    public void ConfirmationRequiredDoesNotBecomeAvailableTests()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        registry.GetRequired("executor.wait_ticks").ConfirmationPolicy =
            OptionConfirmationPolicy.ExplicitUserConfirmationRequired;
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator(
                registry,
                new StardewAI.Core.Verifier.Verifier())
            .Evaluate(
                Snapshot(100, Field("time", "time", "600")),
                new[] { Candidate("executor.wait_ticks", ("wait_ticks", "10")) },
                includeExecutorCalibrationOptions: true)
            .Options);

        Assert.True(option.ReadEligible);
        Assert.Equal("bound", option.BindingStatus);
        Assert.Equal("ready", option.CompileStatus);
        Assert.Equal("confirmation_required", option.ExecutionAuthorization);
        Assert.False(option.Available);
        Assert.Contains("explicit_user_confirmation_required", option.BlockingReasons);
    }

    [Fact]
    public void HostOnlyOptionBlockedForNonHostTests()
    {
        var result = SafetyPolicyGate.Evaluate(
            Registry("executor.purchase_joja_membership"),
            new OptionAvailabilityCandidate { ActorIsHost = false });

        Assert.Equal("denied", result.ExecutionAuthorization);
        Assert.Contains("host_only_option_requires_host_actor", result.BlockingReasons);
    }

    [Fact]
    public void FeasibleDoesNotImplyExecutableTests()
    {
        var preview = new PlanningPreviewCompiler().Compile(
            Snapshot(
                100,
                Fields(
                    ("time", "season", "\"spring\""),
                    ("time", "weather", "\"sun\""),
                    ("player", "location_id", "\"Farm\""),
                    ("player", "tile_x", "6"),
                    ("player", "tile_y", "8"),
                    ("player", "energy", "270"),
                    ("player", "inventory", "[]"),
                    ("current_location", "crops", "[]"),
                    ("current_location", "planting_context", "{\"hoe_dirt_tiles\":[]}"),
                    ("locations", "collision_grid", "{\"width\":80,\"height\":65,\"notable_tiles\":[]}"),
                    ("menus", "active_menu", "{\"is_open\":false}"))),
            "water crops today",
            "efficiency");

        Assert.True(preview.WouldBeReadEligible);
        Assert.True(preview.WouldBind);
        Assert.False(preview.WouldCompile);
        Assert.False(preview.WouldBeExecutable);
    }

    private static OptionSpec Registry(string optionId)
    {
        return new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired(optionId);
    }

    private static OptionAvailability Evaluate(
        SnapshotEnvelope snapshot,
        OptionAvailabilityCandidate candidate)
    {
        return Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { candidate }, includeExecutorCalibrationOptions: true)
            .Options);
    }

    private static OptionAvailabilityCandidate Candidate(
        string optionId,
        params (string Name, string Value)[] parameters)
    {
        return new OptionAvailabilityCandidate
        {
            OptionId = optionId,
            Parameters = parameters.Select(row => new SmallModelActionParameter
            {
                Name = row.Name,
                Value = row.Value
            }).ToArray()
        };
    }

    private static SnapshotEnvelope Snapshot(long tick, params string[] fields)
    {
        var grouped = fields
            .Select(field => JsonSerializer.Deserialize<JsonElement>(field))
            .GroupBy(field => field.GetProperty("section").GetString()!)
            .ToDictionary(
                group => group.Key,
                group => "{" + string.Join(",", group.Select(field =>
                    JsonSerializer.Serialize(field.GetProperty("name").GetString()) + ":" +
                    field.GetProperty("envelope").GetRawText())) + "}");
        var stateJson = "{" + string.Join(",", grouped.Select(pair =>
            JsonSerializer.Serialize(pair.Key) + ":" + pair.Value)) + "}";
        return new SnapshotEnvelope
        {
            BridgeVersion = "test",
            GameVersion = "1.6.15",
            GameTick = tick,
            StateHash = "hash-" + tick,
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!
        };
    }

    private static string[] Fields(params (string Section, string Name, string Value)[] fields)
    {
        return fields.Select(row => Field(row.Section, row.Name, row.Value)).ToArray();
    }

    private static string Field(
        string section,
        string name,
        string value,
        string status = "available",
        long readAtTick = 100,
        string sourceKind = "game_object",
        string adapter = "test",
        string? derivation = null)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["value"] = JsonSerializer.Deserialize<JsonElement>(value),
            ["status"] = status,
            ["source"] = new { kind = sourceKind, path = "test" },
            ["adapter"] = adapter,
            ["read_at_tick"] = readAtTick,
            ["confidence"] = 1.0
        };
        if (derivation is not null)
        {
            envelope["derivation"] = new { method = derivation, inputs = Array.Empty<string>() };
        }

        return JsonSerializer.Serialize(new
        {
            section,
            name,
            envelope
        });
    }
}
