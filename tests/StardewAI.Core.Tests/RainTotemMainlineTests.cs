using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class RainTotemMainlineTests
{
    private const string OptionId = "executor.use_rain_totem";
    private const string NativeContract =
        "Object.performUseAction((O)681)->Object.rainTotem->AllowRainTotem->RainTotemAffectsContext_or_location_context->Default_festival_guard_or_context_WeatherForTomorrow=Rain->Default_Game1.getWeatherModificationsForDate";

    [Theory]
    [InlineData("Farm", "Default", "", "Default", "Default")]
    [InlineData("Desert", "Desert", "Default", "Default", "Default")]
    [InlineData("IslandSouth", "Island", "", "Island", "Island")]
    [InlineData("ModRainRoom", "ModSource", "ModTarget", "ModTarget", "ModSource")]
    public void ExactNativeUseCompilesForEveryWeatherRoutingBranch(
        string location,
        string sourceContext,
        string configuredAffectedContext,
        string affectedContext,
        string weatherStateOwnerContext)
    {
        var snapshot = Snapshot(location, sourceContext, configuredAffectedContext, affectedContext,
            weatherStateOwnerContext: weatherStateOwnerContext);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, location, sourceContext, configuredAffectedContext, affectedContext,
                weatherStateOwnerContext: weatherStateOwnerContext), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_rain_totem", step.StepType);
        Assert.Equal(location + ":slot2:(O)681", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("affected_context=" + affectedContext, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("weather_for_tomorrow=Rain", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rain_totem_projection_fingerprint", "drifted", "use_rain_totem_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_after", "2", "use_rain_totem_inventory_identity_drifted")]
    [InlineData("source_location_context_id", "Island", "use_rain_totem_context_routing_drifted")]
    [InlineData("affected_location_context_id", "Island", "use_rain_totem_context_routing_drifted")]
    [InlineData("allow_rain_totem", "false", "use_rain_totem_context_routing_drifted")]
    [InlineData("affected_weather_before", "Rain", "use_rain_totem_weather_state_drifted")]
    [InlineData("native_animation_duration_ms", "0", "use_rain_totem_animation_contract_drifted")]
    [InlineData("native_contract", "direct_weather_write", "use_rain_totem_native_contract_drifted")]
    public void StaleInventoryContextWeatherAndAnimationClaimsFailClosed(
        string parameter,
        string value,
        string reason)
    {
        var snapshot = Snapshot("Farm", "Default", "", "Default");
        var request = Request(snapshot, "Farm", "Default", "", "Default");
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains(reason, item.BlockingReasons);
    }

    [Theory]
    [InlineData(true, false, "Festival", "blocked_default_festival_tomorrow")]
    [InlineData(false, true, "Rain", "blocked_weather_already_rain")]
    [InlineData(false, false, "Sun", "blocked_tomorrow_weather_override")]
    public void WastefulNativeConsumptionBranchesAreExcludedBeforeExecution(
        bool tomorrowFestival,
        bool alreadyRain,
        string effectiveTomorrowWeather,
        string expectedGate)
    {
        var snapshot = Snapshot("Farm", "Default", "", "Default", tomorrowFestival, alreadyRain,
            effectiveTomorrowWeather: effectiveTomorrowWeather);
        var request = Request(snapshot, "Farm", "Default", "", "Default", tomorrowFestival, alreadyRain,
            effectiveTomorrowWeather: effectiveTomorrowWeather);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("use_rain_totem_native_effect_gate_blocked", item.BlockingReasons);
        Assert.Contains(expectedGate, snapshot.State["player"].GetProperty("rain_totem")
            .GetProperty("value").GetProperty("native_use_gate_status").GetString());
    }

    [Fact]
    public void RainTotemClosesFiveGatesAsMechanicalExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-288" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-288" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-288" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-288" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-288" }, capability.OutputEvidenceIds);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(OptionInvocationPolicy.PolicyOrAutonomous, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.NotPolicyTrainingOption, capability.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired(OptionId).PrimaryEngineId);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void RuntimeUsesSharedNativeObjectUseAndNeverWritesWeatherOrInventoryDirectly()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.RainTotem.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.RainTotem.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeRainTotemSmoke.ps1"));

        Assert.Contains("UseInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("AllowRainTotem", projection, StringComparison.Ordinal);
        Assert.Contains("RainTotemAffectsContext", projection, StringComparison.Ordinal);
        Assert.Contains("Utility.isFestivalDay", projection, StringComparison.Ordinal);
        Assert.Contains("Farmer.canMoveNow", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("forceCanMove", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherForTomorrow =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("weatherForTomorrow =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("broadcastSprites(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("playSound(", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot,
        string location,
        string sourceContext,
        string configuredAffectedContext,
        string affectedContext,
        bool tomorrowFestival = false,
        bool alreadyRain = false,
        string? weatherStateOwnerContext = null,
        string effectiveTomorrowWeather = "Rain") => new()
    {
        ModelOutputId = "rain-totem-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.crop_weather_planning",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.test",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "use-rain-totem",
                OptionId = OptionId,
                Rationale = "day planner selected exact native tomorrow-rain transition",
                Parameters = new[]
                {
                    P("target_location", location), P("inventory_slot_index", "2"),
                    P("item_id", "681"), P("qualified_item_id", "(O)681"),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "2"), P("inventory_stack_after", "1"),
                    P("rain_totem_projection_fingerprint", "rain-totem-fingerprint"),
                    P("source_location_context_id", sourceContext),
                    P("configured_affected_context_id", configuredAffectedContext),
                    P("affected_location_context_id", affectedContext),
                    P("weather_state_owner_context_id", weatherStateOwnerContext ?? affectedContext),
                    P("allow_rain_totem", "true"),
                    P("tomorrow_is_default_festival", tomorrowFestival.ToString().ToLowerInvariant()),
                    P("affected_weather_before", alreadyRain ? "Rain" : "Sun"),
                    P("affected_weather_after", "Rain"),
                    P("tomorrow_total_days", "100"),
                    P("effective_tomorrow_weather", effectiveTomorrowWeather),
                    P("rain_will_take_effect_tomorrow", (effectiveTomorrowWeather == "Rain").ToString().ToLowerInvariant()),
                    P("native_facing_direction", "2"), P("native_animation_duration_ms", "2000"),
                    P("native_cloud_sprite_count", "18"), P("native_item_sprite_count", "1"),
                    P("native_cloud_batch_count", "6"), P("native_cloud_delay_step_ms", "200"),
                    P("native_initial_sound", "thunder"), P("native_delayed_sound", "rainsound"),
                    P("native_delayed_sound_ms", "2000"), P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string location,
        string sourceContext,
        string configuredAffectedContext,
        string affectedContext,
        bool tomorrowFestival = false,
        bool alreadyRain = false,
        string? weatherStateOwnerContext = null,
        string effectiveTomorrowWeather = "Rain")
    {
        var weather = alreadyRain ? "Rain" : "Sun";
        var gate = tomorrowFestival ? "blocked_default_festival_tomorrow" :
            alreadyRain ? "blocked_weather_already_rain" :
            effectiveTomorrowWeather != "Rain" ? "blocked_tomorrow_weather_override" : "ready";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"{{{location}}}","status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"681","qualified_item_id":"(O)681","stack":2}],"status":"available"},
            "rain_totem":{"value":{
              "projection_fingerprint":"rain-totem-fingerprint","native_use_gate_status":"{{{gate}}}",
              "native_contract":"{{{NativeContract}}}",
              "context_routing":{"source_location_context_id":"{{{sourceContext}}}",
                "configured_affected_context_id":"{{{configuredAffectedContext}}}",
                "affected_location_context_id":"{{{affectedContext}}}",
                "weather_state_owner_context_id":"{{{weatherStateOwnerContext ?? affectedContext}}}","allow_rain_totem":true},
              "weather_transition":{"tomorrow_is_default_festival":{{{tomorrowFestival.ToString().ToLowerInvariant()}}},
                "affected_weather_before":"{{{weather}}}","affected_weather_after":"Rain",
                "tomorrow_total_days":100,"effective_tomorrow_weather":"{{{effectiveTomorrowWeather}}}",
                "rain_will_take_effect_tomorrow":{{{(effectiveTomorrowWeather == "Rain").ToString().ToLowerInvariant()}}}},
              "animation_contract":{"facing_direction":2,"animation_duration_ms":2000,
                "cloud_sprite_count":18,"item_sprite_count":1,"cloud_batch_count":6,"cloud_delay_step_ms":200,
                "initial_sound":"thunder","delayed_sound":"rainsound","delayed_sound_ms":2000},
              "rows":[{"inventory_slot_index":2,"item_id":"681","qualified_item_id":"(O)681",
                "inventory_runtime_type":"StardewValley.Object","stack_before":2,"stack_after":1,"temporarily_invisible":false}]
            },"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-29T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
