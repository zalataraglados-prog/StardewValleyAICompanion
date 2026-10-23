using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] GingerIslandMaterialSupplyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string repairKey,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var repair = access.GetProperty("repairs").GetProperty(repairKey);
        var qualifiedItemId = ReadString(repair, "qualified_item_id");
        var requiredCount = ReadInt(repair, "required_count");
        var availableCount = ReadInt(repair, "available_count");
        var deficitCount = Math.Max(0, requiredCount - availableCount);
        if (deficitCount == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_material_projection_requires_refresh:" + repairKey,
                    intentParameters)
            };
        }

        var producers = new List<EventCandidate>();
        producers.AddRange(MachineServiceCandidates(snapshot, null)
            .Where(candidate => candidate.Available &&
                candidate.Kind is ("collect_machine_output_tile" or "route_connector_tile") &&
                string.Equals(candidate.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)));

        if (string.Equals(qualifiedItemId, "(O)709", StringComparison.Ordinal))
        {
            producers.AddRange(WildTreeChopAcquisitionCandidates(snapshot)
                .Where(candidate => candidate.Available && CandidateGuaranteesItem(candidate, qualifiedItemId)));
            producers.AddRange(ClearFarmResourceClumpCandidates(snapshot)
                .Where(candidate => candidate.Available && CandidateGuaranteesItem(candidate, qualifiedItemId)));
        }

        var candidates = producers
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => BindGingerIslandSupplyCandidate(
                group.First(),
                "material-" + repairKey,
                qualifiedItemId,
                requiredCount,
                availableCount,
                deficitCount,
                intentParameters,
                continuation))
            .OrderBy(candidate => candidate.EstimatedTicks <= 0 ? int.MaxValue : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_exact_material_producer:" + qualifiedItemId,
                    intentParameters)
            };
    }

    private EventCandidate[] GingerIslandWalnutSupplyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        string perchKey,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var perch = access.GetProperty(perchKey);
        var requiredCount = ReadInt(perch, "required_nuts");
        var availableCount = ReadInt(access.GetProperty("walnuts"), "current");
        var deficitCount = Math.Max(0, requiredCount - availableCount);
        if (deficitCount == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_walnut_projection_requires_refresh:" + perchKey,
                    intentParameters)
            };
        }

        var producers = BushHarvestCandidates(snapshot)
            .Concat(FishingEventCandidateBuilder.Build(snapshot))
            .Concat(SpawnedObjectForagingCandidates(snapshot))
            .Concat(ArtifactSpotExcavationCandidates(snapshot))
            .Concat(GeodeProcessingCandidates(snapshot, Array.Empty<SmallModelActionParameter>()))
            .Concat(FieldOfficeSurveyCandidates(snapshot))
            .Where(candidate => candidate.Available)
            .Select(candidate => new
            {
                Candidate = candidate,
                Quantity = GingerIslandWalnutQuantity(candidate)
            })
            .Where(source => source.Quantity > 0)
            .GroupBy(source => source.Candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(source => source.Candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : source.Candidate.EstimatedTicks)
            .ThenBy(source => source.Candidate.CandidateId, StringComparer.Ordinal)
            .Select(source => BindGingerIslandSupplyCandidate(
                source.Candidate,
                "walnuts-" + perchKey,
                "golden-walnut",
                requiredCount,
                availableCount,
                deficitCount,
                intentParameters,
                continuation,
                projectedQuantity: source.Quantity,
                producerProjection: GingerIslandWalnutProducerProjection(source.Candidate)))
            .ToArray();

        if (producers.Length > 0)
            return producers;

        var catalog = ReadStateFieldValue(snapshot, "world_progress", "golden_walnut_sources");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Object ||
            ReadString(catalog.Value, "schema_version") != "golden_walnut_sources.v1" ||
            ReadString(catalog.Value, "projection_status") !=
                "complete_locked_base_1.6.15_island_hut_denominator")
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_walnut_source_catalog_missing",
                    intentParameters)
            };
        }

        var currentCatalogSources = GingerIslandWalnutCatalogCurrentCandidates(
            snapshot,
            catalog.Value,
            perchKey,
            requiredCount,
            availableCount,
            deficitCount,
            intentParameters,
            continuation);
        if (currentCatalogSources.Length > 0)
            return currentCatalogSources;

        var routes = GingerIslandWalnutCatalogRouteCandidates(
            snapshot,
            catalog.Value,
            perchKey,
            requiredCount,
            availableCount,
            deficitCount,
            intentParameters,
            continuation);
        return routes.Length > 0
            ? routes
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_exact_spend_safe_walnut_route:" + perchKey,
                    intentParameters)
            };
    }

    private EventCandidate[] GingerIslandWalnutCatalogCurrentCandidates(
        SnapshotEnvelope snapshot,
        JsonElement catalog,
        string perchKey,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        if (!catalog.TryGetProperty("sources", out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        return sources.EnumerateArray()
            .Where(source => source.ValueKind == JsonValueKind.Object &&
                ReadInt(source, "remaining_quantity") > 0 &&
                ReadBool(source, "pre_resort_eligible") == true &&
                ReadBool(source, "spend_safe_before_resort") == true &&
                ReadBool(source, "required_mail_satisfied") == true &&
                GingerIslandWalnutSecretNoteSatisfied(source) &&
                string.Equals(
                    ReadString(source, "location_id"),
                    currentLocation,
                    StringComparison.OrdinalIgnoreCase) &&
                ReadString(source, "action_binding_status") == "existing_action_family_bound")
            .SelectMany(source => GingerIslandWalnutCurrentActionCandidates(snapshot, source)
                .Where(candidate => candidate.Available)
                .Select(candidate => BindGingerIslandWalnutCurrentSourceCandidate(
                    candidate,
                    source,
                    perchKey,
                    requiredCount,
                    availableCount,
                    deficitCount,
                    intentParameters,
                    continuation)))
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.EstimatedTicks <= 0 ? int.MaxValue : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private EventCandidate[] GingerIslandWalnutCurrentActionCandidates(
        SnapshotEnvelope snapshot,
        JsonElement source)
    {
        return ReadString(source, "acquisition_option_id") switch
        {
            "fishing.catch_fish" => FishingEventCandidateBuilder.Build(snapshot)
                .Where(candidate => candidate.Kind == "catch_fish")
                .ToArray(),
            "executor.clear_obstacle" => ClearObstacleCandidates(snapshot)
                .Where(candidate =>
                    candidate.Kind == "clear_obstacle_tile" &&
                    ReadParameter(candidate.Parameters, "clear_source_id") == "(O)25" &&
                    ReadParameter(candidate.Parameters, "clear_output_projection_status") ==
                        "native_stochastic_receipt")
                .ToArray(),
            "farm.maintain_crops" => FarmMaintenanceCandidates(snapshot)
                .Where(candidate => candidate.Kind == "harvest_crop_tile")
                .ToArray(),
            "executor.till_soil" => GingerIslandBuriedNutCandidates(snapshot, source),
            _ => Array.Empty<EventCandidate>()
        };
    }

    private EventCandidate[] GingerIslandBuriedNutCandidates(
        SnapshotEnvelope snapshot,
        JsonElement source)
    {
        var targetX = ReadNullableInt(source, "tile_x");
        var targetY = ReadNullableInt(source, "tile_y");
        if (!targetX.HasValue || !targetY.HasValue)
            return Array.Empty<EventCandidate>();

        var locationId = ReadString(source, "location_id");
        var sourceKey = ReadString(source, "source_key");
        var stand = FindBestStandTile(snapshot, targetX.Value, targetY.Value);
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        var hoeSlot = inventory.HasValue && inventory.Value.ValueKind == JsonValueKind.Array
            ? inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(item, "qualified_item_id"),
                        "(T)Hoe",
                        StringComparison.Ordinal))
                .Select(item => ReadNullableInt(item, "slot_index"))
                .FirstOrDefault(slot => slot.HasValue)
            : null;
        var parameters = new List<SmallModelActionParameter>
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", targetX.Value.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", targetY.Value.ToString(CultureInfo.InvariantCulture)),
            Parameter("required_tool_kind", "hoe"),
            Parameter("tool_slot_index", hoeSlot?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("buried_nut_source_key", sourceKey),
            Parameter("buried_nut_runtime_source_validation_status", ReadString(source, "runtime_source_validation_status")),
            Parameter("fresh_snapshot_rebind_required", "true"),
            Parameter("max_movement_tiles", "512")
        };
        if (stand is not null)
        {
            parameters.Add(Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)));
            parameters.Add(Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)));
            parameters.Add(Parameter("route_distance_tiles", stand.Distance.ToString(CultureInfo.InvariantCulture)));
        }

        var blockReasons = new List<string>();
        var runtimeValidationStatus = ReadString(source, "runtime_source_validation_status");
        if (runtimeValidationStatus is not
            ("exact_current_world_buried_nut_point_present" or
             "exact_current_world_journal_buried_tile_present_and_note_seen"))
        {
            blockReasons.Add("buried_nut_runtime_source_not_exact");
        }
        if (!hoeSlot.HasValue)
            blockReasons.Add("buried_nut_required_hoe_missing");
        if (stand is null)
            blockReasons.Add("buried_nut_no_adjacent_route_stand_tile");
        if (ReadStateFieldDoubleOptional(snapshot, "player", "energy") is not { } energy)
            blockReasons.Add("buried_nut_player_energy_unavailable");
        else if (energy <= 0d)
            blockReasons.Add("buried_nut_insufficient_stamina");
        if (ActiveMenuOpenForCandidate(snapshot))
            blockReasons.Add("buried_nut_menu_must_be_clear");

        var estimatedTicks = Math.Max(60, (stand?.Distance ?? 0) * 60 + 60);
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "buried-nut:" + sourceKey,
                Kind = "till_soil_tile",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = targetX,
                TileY = targetY,
                QualifiedItemId = "(O)73",
                Quantity = 1,
                ExpectedEffect =
                    "current_location.terrain_features[" + targetX.Value + "," + targetY.Value + "].type=HoeDirt;" +
                    "native_tool=Hoe;collected_nut_tracker_key=" + sourceKey + ";fresh_snapshot_rebind_required=true",
                EstimatedTicks = estimatedTicks,
                EnergyCost = 2,
                AvailabilityClass = "exact_current_world_buried_nut_point",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters.ToArray()
            }
        };
    }

    private static EventCandidate BindGingerIslandWalnutCurrentSourceCandidate(
        EventCandidate candidate,
        JsonElement source,
        string perchKey,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var sourceKey = ReadString(source, "source_key");
        var trackingKind = ReadString(source, "tracking_kind");
        var probabilityStatus = ReadString(source, "probability_status");
        var exactOneTimeReceipt = string.Equals(trackingKind, "tracker", StringComparison.Ordinal) ||
            probabilityStatus.StartsWith("exact_native", StringComparison.Ordinal);
        var trackerReceipt = string.Equals(trackingKind, "tracker", StringComparison.Ordinal);
        var projectedQuantity = exactOneTimeReceipt
            ? Math.Min(
                ReadInt(source, "native_quantity"),
                ReadInt(source, "remaining_quantity"))
            : 0;
        var receipt = trackerReceipt
            ? "collected_nut_tracker_fresh_snapshot"
            : "limited_nut_drop_counter_fresh_snapshot";
        var replanPolicy = trackerReceipt
            ? "execute_once_then_fresh_collected_nut_tracker"
            : exactOneTimeReceipt
                ? "execute_once_then_fresh_limited_drop_counter"
                : "execute_one_native_attempt_then_fresh_limited_drop_counter";
        var bound = BindGingerIslandSupplyCandidate(
            candidate,
            "walnuts-" + perchKey,
            "golden-walnut",
            requiredCount,
            availableCount,
            deficitCount,
            intentParameters,
            continuation,
            projectedQuantity: projectedQuantity,
            producerProjection: exactOneTimeReceipt
                ? "exact_native_one_time_tracker_receipt"
                : "bounded_native_stochastic_retry_source");
        return CloneCandidate(
            bound,
            candidateId: bound.CandidateId + ":source:" + sourceKey,
            expectedEffect: bound.ExpectedEffect +
                ";ginger_island_walnut_source_key=" + sourceKey +
                ";ginger_island_walnut_source_remaining_quantity=" +
                ReadInt(source, "remaining_quantity") +
                ";ginger_island_walnut_retry_receipt=" + receipt,
            parameters: bound.Parameters.Concat(new[]
            {
                Parameter("ginger_island_walnut_source_key", sourceKey),
                Parameter("ginger_island_walnut_source_tracking_kind", trackingKind),
                Parameter("ginger_island_walnut_source_location", ReadString(source, "location_id")),
                Parameter("ginger_island_walnut_source_native_quantity", ReadInt(source, "native_quantity").ToString(CultureInfo.InvariantCulture)),
                Parameter("ginger_island_walnut_source_remaining_quantity", ReadInt(source, "remaining_quantity").ToString(CultureInfo.InvariantCulture)),
                Parameter("ginger_island_walnut_source_acquisition_option_id", ReadString(source, "acquisition_option_id")),
                Parameter("ginger_island_walnut_source_probability_status", ReadString(source, "probability_status")),
                Parameter("ginger_island_walnut_source_replan_policy", replanPolicy)
            }).ToArray(),
            availabilityClass: "master_angler_ginger_island_walnut_current_source");
    }

    private EventCandidate[] GingerIslandWalnutCatalogRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement catalog,
        string perchKey,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        if (!catalog.TryGetProperty("sources", out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var connectorCandidates = RouteConnectorCandidates(snapshot, int.MaxValue);
        return sources.EnumerateArray()
            .Where(source => source.ValueKind == JsonValueKind.Object &&
                ReadInt(source, "remaining_quantity") > 0 &&
                ReadBool(source, "pre_resort_eligible") == true &&
                ReadBool(source, "spend_safe_before_resort") == true &&
                ReadBool(source, "required_mail_satisfied") == true &&
                GingerIslandWalnutSecretNoteSatisfied(source) &&
                GingerIslandWalnutCrossMapAcquisitionBound(
                    ReadString(source, "acquisition_option_id")) &&
                ReadString(source, "action_binding_status") == "existing_action_family_bound")
            .Select(source => new
            {
                Source = source,
                LocationId = ReadString(source, "location_id"),
                SourceKey = ReadString(source, "source_key"),
                NativeQuantity = ReadInt(source, "native_quantity"),
                ProbabilityStatus = ReadString(source, "probability_status")
            })
            .Where(source =>
                !string.IsNullOrWhiteSpace(source.LocationId) &&
                !string.Equals(source.LocationId, currentLocation, StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => source.ProbabilityStatus.StartsWith("exact_native", StringComparison.Ordinal)
                ? 0
                : 1)
            .ThenBy(source => source.SourceKey, StringComparer.Ordinal)
            .GroupBy(source => source.LocationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(source => new
            {
                source.Source,
                source.LocationId,
                source.SourceKey,
                source.NativeQuantity,
                Route = FindResolvedRoutePlan(
                    snapshot,
                    currentLocation,
                    source.LocationId,
                    connectorCandidates)?.FirstActionCandidate
            })
            .Where(source => source.Route?.Available == true)
            .Select(source => BindGingerIslandWalnutRouteCandidate(
                source.Route!,
                source.Source,
                source.SourceKey,
                source.LocationId,
                source.NativeQuantity,
                perchKey,
                requiredCount,
                availableCount,
                deficitCount,
                intentParameters,
                continuation))
            .OrderBy(candidate => candidate.EstimatedTicks <= 0 ? int.MaxValue : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool GingerIslandWalnutSecretNoteSatisfied(JsonElement source) =>
        !source.TryGetProperty("required_secret_note_satisfied", out var satisfied) ||
        satisfied.ValueKind == JsonValueKind.True;

    private static bool GingerIslandWalnutCrossMapAcquisitionBound(string optionId) =>
        optionId is "foraging.harvest_bushes" or
            "fishing.catch_fish" or
            "executor.clear_obstacle" or
            "executor.till_soil";

    private static EventCandidate BindGingerIslandWalnutRouteCandidate(
        EventCandidate route,
        JsonElement source,
        string sourceKey,
        string sourceLocation,
        int nativeQuantity,
        string perchKey,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var stage = "walnuts-" + perchKey;
        var bound = BindGingerIslandSupplyCandidate(
            route,
            stage,
            "golden-walnut",
            requiredCount,
            availableCount,
            deficitCount,
            intentParameters,
            continuation,
            projectedQuantity: 0,
            producerProjection: "exact_native_cross_map_source_route_then_fresh_replan");
        return CloneCandidate(
            bound,
            candidateId: bound.CandidateId + ":source:" + sourceKey,
            expectedEffect: bound.ExpectedEffect +
                ";ginger_island_walnut_source_key=" + sourceKey +
                ";ginger_island_walnut_source_location=" + sourceLocation +
                ";ginger_island_walnut_source_native_quantity=" + nativeQuantity,
            parameters: bound.Parameters.Concat(new[]
            {
                Parameter("ginger_island_walnut_source_key", sourceKey),
                Parameter("ginger_island_walnut_source_tracking_kind", ReadString(source, "tracking_kind")),
                Parameter("ginger_island_walnut_source_location", sourceLocation),
                Parameter("ginger_island_walnut_source_tile_x", ReadNullableInt(source, "tile_x")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Parameter("ginger_island_walnut_source_tile_y", ReadNullableInt(source, "tile_y")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Parameter("ginger_island_walnut_source_native_quantity", nativeQuantity.ToString(CultureInfo.InvariantCulture)),
                Parameter("ginger_island_walnut_source_acquisition_option_id", ReadString(source, "acquisition_option_id")),
                Parameter("ginger_island_walnut_source_probability_status", ReadString(source, "probability_status")),
                Parameter("ginger_island_walnut_source_replan_policy", "route_one_connector_then_fresh_current_location_candidate")
            }).ToArray(),
            availabilityClass: "master_angler_ginger_island_walnut_source_route");
    }

    private static int GingerIslandWalnutQuantity(EventCandidate candidate)
    {
        const string walnutQualifiedItemId = "(O)73";
        if (candidate.Kind is ("harvest_bush" or "catch_fish" or "collect_spawned_object") &&
            string.Equals(candidate.QualifiedItemId, walnutQualifiedItemId, StringComparison.Ordinal))
        {
            return Math.Max(1, candidate.Quantity);
        }

        if (candidate.Kind == "clear_obstacle_tile")
        {
            var jsonQuantity = CandidateJsonItemQuantity(
                ReadParameter(candidate.Parameters, "clear_output_items_json"),
                walnutQualifiedItemId);
            if (jsonQuantity > 0)
                return jsonQuantity;
            if (ReadParameter(candidate.Parameters, "clear_output_qualified_item_id") == walnutQualifiedItemId)
            {
                var minimum = ReadIntParameter(candidate.Parameters, "clear_output_quantity_min");
                var maximum = ReadIntParameter(candidate.Parameters, "clear_output_quantity_max");
                return minimum > 0 && minimum == maximum ? minimum : 0;
            }
        }

        if (candidate.Kind == "crack_geode" &&
            ReadParameter(candidate.Parameters, "geode_expected_output_qid") == walnutQualifiedItemId)
        {
            return Math.Max(0, ReadIntParameter(candidate.Parameters, "geode_expected_output_stack"));
        }

        return candidate.Kind == "answer_field_office_survey"
            ? Math.Max(0, ReadIntParameter(candidate.Parameters, "golden_walnuts_found_delta"))
            : 0;
    }

    private static string GingerIslandWalnutProducerProjection(EventCandidate candidate) =>
        candidate.Kind == "catch_fish" &&
        string.Equals(
            ReadParameter(candidate.Parameters, "result_is_stochastic"),
            "true",
            StringComparison.OrdinalIgnoreCase)
            ? "exact_native_stochastic_retry_source"
            : "exact_native_current_snapshot_receipt";

    private EventCandidate[] GingerIslandTicketMoneyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement access,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation)
    {
        var money = ReadInt(access.GetProperty("ticket"), "money_available");
        var deficit = Math.Max(0, 1000 - money);
        if (deficit == 0)
        {
            return new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_ticket_money_projection_requires_refresh",
                    intentParameters)
            };
        }

        var pendingSettlement = GingerIslandPendingShippingSettlement(snapshot);
        EventCandidate[] sources;
        string stage;
        if (pendingSettlement >= deficit)
        {
            sources = RecoveryCandidates(
                snapshot,
                new[] { Parameter("control_plane.native_save_boundary", "true") });
            stage = "ticket-money-settlement";
        }
        else
        {
            sources = ShipItemStageCandidates(snapshot, Array.Empty<SmallModelActionParameter>())
                .Where(candidate => candidate.Available && candidate.TotalValue > 0)
                .ToArray();
            stage = "ticket-money-shipping";
        }

        var candidates = sources
            .Select(candidate => BindGingerIslandSupplyCandidate(
                candidate,
                stage,
                "money",
                1000,
                money,
                deficit,
                intentParameters,
                continuation,
                pendingSettlement))
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : new[]
            {
                BlockedGingerIslandCandidate(
                    "master_angler_ginger_island_no_safe_ticket_money_producer",
                    intentParameters)
            };
    }

    private static EventCandidate BindGingerIslandSupplyCandidate(
        EventCandidate candidate,
        string stage,
        string resourceId,
        int requiredCount,
        int availableCount,
        int deficitCount,
        SmallModelActionParameter[] intentParameters,
        SmallModelActionParameter[] continuation,
        int pendingSettlement = 0,
        int projectedQuantity = 0,
        string producerProjection = "exact_native_current_snapshot_receipt") => CloneCandidate(
            candidate,
            candidateId: "master-angler-ginger-island:" + stage + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";ginger_island_supply_stage=" + stage +
                ";ginger_island_supply_resource=" + resourceId +
                ";ginger_island_supply_deficit=" + deficitCount +
                ";ginger_island_supply_projected_quantity=" + projectedQuantity +
                ";ginger_island_supply_producer_projection=" + producerProjection +
                ";fresh_snapshot_and_ginger_island_replan_required=true",
            parameters: candidate.Parameters
                .Where(parameter => !parameter.Name.StartsWith("continuation.", StringComparison.Ordinal))
                .Concat(intentParameters)
                .Concat(continuation)
                .Concat(new[]
                {
                    Parameter("ginger_island_supply_stage", stage),
                    Parameter("ginger_island_supply_resource", resourceId),
                    Parameter("ginger_island_supply_required_count", requiredCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_available_count", availableCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_deficit_count", deficitCount.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_pending_shipping_settlement", pendingSettlement.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_projected_quantity", projectedQuantity.ToString(CultureInfo.InvariantCulture)),
                    Parameter("ginger_island_supply_producer_projection", producerProjection)
                })
                .ToArray(),
            availabilityClass: "master_angler_ginger_island_" + stage);

    private static int GingerIslandPendingShippingSettlement(SnapshotEnvelope snapshot)
    {
        var bins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
        if (!bins.HasValue || bins.Value.ValueKind != JsonValueKind.Array)
            return 0;
        // Every completed bin projects the same native shipping inventory aggregate.
        return bins.Value.EnumerateArray()
            .Where(bin => bin.ValueKind == JsonValueKind.Object &&
                ReadBool(bin, "pending_settlement_value_complete") == true)
            .Select(bin => Math.Max(0, ReadInt(bin, "pending_settlement_value")))
            .DefaultIfEmpty(0)
            .Max();
    }
}
