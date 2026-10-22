using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void FutureRainWindowProjectsOnlyRequiredPineTapperCapacity()
    {
        using var fixture = RainTotemReserveFixture.Create();

        var produced = MasterAnglerWindowIntentValidator
            .TryBuildRainTotemReserveIntent(
                fixture.Snapshot,
                fixture.WindowIndexPath,
                fixture.WindowIndexSha256,
                out var parameters,
                out var validation,
                out var rejectionReason);

        Assert.True(produced, rejectionReason);
        Assert.Equal(111, validation.TargetTotalDay);
        Assert.Equal(110, validation.UseTotalDay);
        Assert.Equal(new[] { "(O)145" }, validation.TargetQualifiedItemIds);
        Assert.Equal(2, validation.OutputQuantityPerNewTapper);
        Assert.Equal(3, validation.RequiredAdditionalTapperCount);
        Assert.True(
            MasterAnglerWindowIntentValidator.TryValidateRainTotemReserveIntent(
                fixture.Snapshot,
                parameters,
                out _,
                out rejectionReason),
            rejectionReason);
    }

    [Fact]
    public void ExistingPineTarAndExactPendingPineTapperOutputReduceCapacity()
    {
        using var fixture = RainTotemReserveFixture.Create(
            pineTarInventory: 1,
            includePendingPineTapperOutput: true);

        var produced = MasterAnglerWindowIntentValidator
            .TryBuildRainTotemReserveIntent(
                fixture.Snapshot,
                fixture.WindowIndexPath,
                fixture.WindowIndexSha256,
                out _,
                out var validation,
                out var rejectionReason);

        Assert.True(produced, rejectionReason);
        Assert.Equal(1, validation.InventoryPineTarQuantity);
        Assert.Equal(1, validation.ExistingTapperProjectedOutputQuantity);
        Assert.Equal(2, validation.RequiredAdditionalTapperCount);
    }

    [Fact]
    public void TamperedPineTapperReserveQuantityFailsClosed()
    {
        using var fixture = RainTotemReserveFixture.Create();
        Assert.True(
            MasterAnglerWindowIntentValidator.TryBuildRainTotemReserveIntent(
                fixture.Snapshot,
                fixture.WindowIndexPath,
                fixture.WindowIndexSha256,
                out var parameters,
                out _,
                out var rejectionReason),
            rejectionReason);
        var tampered = parameters.Select(parameter =>
                parameter.Name == MasterAnglerWindowIntentValidator
                    .WeatherReserveRequiredMachineCountParameter
                    ? RainReserveParameter(parameter.Name, "5")
                    : parameter)
            .ToArray();

        Assert.False(
            MasterAnglerWindowIntentValidator.TryValidateRainTotemReserveIntent(
                fixture.Snapshot,
                tampered,
                out _,
                out rejectionReason));
        Assert.Equal(
            "master_angler_weather_reserve_projection_drifted",
            rejectionReason);
    }

    [Fact]
    public void PineTapperReservePlacesOnExactPineInsteadOfCloserOak()
    {
        using var fixture = RainTotemReserveFixture.Create(
            includeInventoryTapper: true,
            includePlacementProjection: true);
        Assert.True(
            MasterAnglerWindowIntentValidator.TryBuildRainTotemReserveIntent(
                fixture.Snapshot,
                fixture.WindowIndexPath,
                fixture.WindowIndexSha256,
                out var parameters,
                out _,
                out var rejectionReason),
            rejectionReason);
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger:rain-totem-pine-tar-reserve"
        };
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = parameters
                }
            },
            commitmentLedger: ledger);

        var option = Assert.Single(availability.Options);
        Assert.True(
            option.EventCandidates.Length > 0,
            JsonSerializer.Serialize(option));
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("place_machine_item", candidate.Kind);
        Assert.Equal(5, candidate.TileX);
        Assert.Equal(5, candidate.TileY);
        Assert.Contains(
            candidate.Parameters,
            parameter => parameter.Name ==
                    "machine_acquisition_target_source_id" &&
                parameter.Value == "wild_tree:3:0");
    }

    private static SmallModelActionParameter RainReserveParameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private sealed class RainTotemReserveFixture : IDisposable
    {
        private RainTotemReserveFixture(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot,
            string windowIndexPath,
            string windowIndexSha256)
        {
            Snapshot = snapshot;
            WindowIndexPath = windowIndexPath;
            WindowIndexSha256 = windowIndexSha256;
        }

        public StardewAI.Contracts.State.SnapshotEnvelope Snapshot { get; }
        public string WindowIndexPath { get; }
        public string WindowIndexSha256 { get; }

        public static RainTotemReserveFixture Create(
            int pineTarInventory = 0,
            bool includePendingPineTapperOutput = false,
            bool includeInventoryTapper = false,
            bool includePlacementProjection = false)
        {
            var fish = Enumerable.Range(0, 72)
                .Select(index => new
                {
                    qualified_item_id = index == 0
                        ? "(O)145"
                        : "(O)" + (1000 + index),
                    caught = index != 0
                })
                .ToArray();
            var species = fish.Select((item, index) => new
            {
                qualified_item_id = item.qualified_item_id,
                windows = new[]
                {
                    new
                    {
                        source_kind = "location_rule",
                        source_key = index == 0 ? "Beach:0" : "Town:0",
                        location_id = index == 0 ? "Beach" : "Town",
                        first_total_day = index == 0 ? 111 : 100,
                        last_total_day = index == 0 ? 111 : 223,
                        minimum_fishing_level = 0,
                        require_magic_bait = false,
                        training_rod_allowed = true,
                        dynamic_conditions = Array.Empty<string>(),
                        weather_modes = index == 0
                            ? new[] { "rain", "storm" }
                            : new[] { "sun", "rain", "storm" },
                        time_windows = new[]
                        {
                            new { start_time = 600, end_time = 2600 }
                        }
                    }
                }
            }).ToArray();
            var windowIndexPath = Path.Combine(
                Path.GetTempPath(),
                "stardewai-rain-totem-reserve-window-index-" +
                Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(
                windowIndexPath,
                JsonSerializer.Serialize(new
                {
                    schema_version =
                        "master_angler_stage_one_window_index.v1",
                    status =
                        "complete_static_windows_dynamic_execution_pending",
                    game_version = string.Empty,
                    deadline_total_day_exclusive = 224,
                    native_denominator_count = 72,
                    static_window_coverage_complete = true,
                    training_label_eligible = false,
                    species
                }),
                new UTF8Encoding(false));
            var windowIndexSha256 = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(windowIndexPath)))
                .ToLowerInvariant();

            var inventory = new List<object>();
            if (pineTarInventory > 0)
            {
                inventory.Add(new
                {
                    slot_index = 0,
                    item_id = "726",
                    qualified_item_id = "(O)726",
                    stack = pineTarInventory
                });
            }
            if (includeInventoryTapper)
            {
                inventory.Add(new
                {
                    slot_index = 4,
                    item_id = "105",
                    qualified_item_id = "(BC)105",
                    stack = 1
                });
            }
            var machines = includePendingPineTapperOutput
                ? new object[]
                {
                    new
                    {
                        location_id = "Farm",
                        tile_x = 8,
                        tile_y = 8,
                        qualified_item_id = "(BC)105",
                        wild_tree_tapper_production = new
                        {
                            projection_status =
                                "exact_locked_base_pine_tapper_cycle",
                            exact_output_qualified_item_id = "(O)726",
                            current_output_qualified_item_id = "(O)726",
                            current_output_stack = 1,
                            ready_for_harvest = false,
                            minutes_until_ready = 1000,
                            exact_base_cycle_days = 5,
                            source_id = "wild_tree:3:0"
                        }
                    }
                }
                : Array.Empty<object>();
            var placement = includePlacementProjection
                ? JsonSerializer.Serialize(new
                {
                    projection_status =
                        "complete_all_inventory_machines_across_loaded_persistent_locations",
                    static_projection_fingerprint = "tapper-placement-fingerprint",
                    rows = new[]
                    {
                        new
                        {
                            inventory_slot_index = 4,
                            item_id = "105",
                            qualified_item_id = "(BC)105",
                            display_name = "Tapper",
                            stack = 1,
                            runtime_type = "StardewValley.Object",
                            is_cask = false,
                            locations = new[]
                            {
                                new
                                {
                                    location_id = "Farm",
                                    location_is_current = true,
                                    machine_operational_context_valid = true,
                                    placement_probe_status =
                                        "native_legal_tiles_available",
                                    static_legal_tile_count = 2,
                                    static_legal_tile_ranges = new[]
                                    {
                                        new { y = 2, start_x = 2, end_x = 2 },
                                        new { y = 5, start_x = 5, end_x = 5 }
                                    },
                                    native_tapper_target_count = 2,
                                    native_tapper_targets = new object[]
                                    {
                                        TapperTarget(2, 2, "wild_tree:1:0", "(O)724"),
                                        TapperTarget(5, 5, "wild_tree:3:0", "(O)726")
                                    }
                                }
                            }
                        }
                    }
                })
                : "{}";
            var inventoryJson = JsonSerializer.Serialize(inventory);
            var machinesJson = JsonSerializer.Serialize(machines);
            var fishJson = JsonSerializer.Serialize(fish);
            var snapshot = Snapshot($$"""
            {
              "time": {
                "total_days": {"value":100,"status":"available"},
                "time": {"value":600,"status":"available"}
              },
              "player": {
                "location_id": {"value":"Farm","status":"available"},
                "tile_x": {"value":1,"status":"available"},
                "tile_y": {"value":1,"status":"available"},
                "energy": {"value":270,"status":"available"},
                "inventory": {"value":{{inventoryJson}},"status":"available"},
                "inventory_capacity": {"value":{"capacity":12,"occupied_slots":{{inventory.Count}},"free_slots":{{12 - inventory.Count}},"empty_slots":{{12 - inventory.Count}},"has_empty_slot":true},"status":"available"},
                "machine_placement": {"value":{{placement}},"status":"available"},
                "rain_totem": {"value":{
                  "projection_status":"complete_current_native_rain_totem_context",
                  "context_routing":{"affected_location_context_id":"Default","allow_rain_totem":true}
                },"status":"available"}
              },
              "farm": {
                "machines": {"value":{{machinesJson}},"status":"available"}
              },
              "fishing": {
                "location_context": {"value":{},"status":"available"},
                "fishable_tiles": {"value":[],"status":"available"},
                "rod_inventory": {"value":[],"status":"available"},
                "rod_contexts": {"value":[{"has_magic_bait":false,"uses_training_rod":false}],"status":"available"},
                "active_cast_state": {"value":{"is_active":false},"status":"available"}
              },
              "world_progress": {
                "fish_collection_progress": {"value":{
                  "eligible_species_count":72,
                  "caught_eligible_species_count":71,
                  "missing_species_count":1,
                  "items":{{fishJson}}
                },"status":"available"},
                "game_state_query_calendar_state": {"value":{
                  "current_total_day":100,
                  "festival_date_keys":[]
                },"status":"available"}
              },
              "locations": {
                "collision_grid": {"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"},
                "social_route_date_evidence": {"value":{"locations":[
                  {"location_id":"Farm","location_context_id":"Default"},
                  {"location_id":"Beach","location_context_id":"Default"},
                  {"location_id":"Town","location_context_id":"Default"}
                ]},"status":"available"}
              },
              "current_location": {
                "map": {"value":{"location_id":"Farm","width":20,"height":20},"status":"available"}
              },
              "menus": {
                "active_menu": {"value":{"is_open":false},"status":"available"}
              }
            }
            """);
            return new RainTotemReserveFixture(
                snapshot,
                windowIndexPath,
                windowIndexSha256);
        }

        private static object TapperTarget(
            int x,
            int y,
            string sourceId,
            string outputId) => new
            {
                tile_x = x,
                tile_y = y,
                tree_type = sourceId == "wild_tree:3:0" ? "3" : "1",
                growth_stage = 5,
                stump = false,
                tapped = false,
                is_temporary_green_rain_tree = false,
                can_be_tapped = true,
                output_rows = new[]
                {
                    new
                    {
                        source_id = sourceId,
                        item_qualified_ids = new[] { outputId },
                        days_until_ready = 5,
                        condition = string.Empty,
                        previous_item_ids = Array.Empty<string>(),
                        season = string.Empty,
                        days_until_ready_modifier_count = 0
                    }
                },
                projection_status =
                    "native_object_placement_action_compatible_tapper_target"
            };

        public void Dispose()
        {
            if (File.Exists(WindowIndexPath))
                File.Delete(WindowIndexPath);
        }
    }
}
