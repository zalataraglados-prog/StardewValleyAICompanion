using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionLocationRouteTargetResolver
{
    public static AcquisitionLocationTargetResolution Resolve(
        AcquisitionRouteTargetDateFestival route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionLocationRouteSnapshotState state)
    {
        var source = route.UpstreamRoute;
        return source.RouteKind switch
        {
            "harvests_as" => ResolveCrop(source, state),
            "sells" => ResolveShop(staticRoute, state),
            "native_crab_pot_output" => ResolveCrabPot(source, state),
            "native_location_artifact_spot" or
            "native_location_fish_spawn" or
            "native_location_forage_spawn" =>
                ResolveLocationSource(source, state),
            "native_mine_fishing_override" => Exact(
                "native_mine_location",
                source.SourceId,
                "UndergroundMine",
                "static_calendar_resolution.routes[].matching_windows[].location_id"),
            _ => new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "location_route_kind_not_supported:" + source.RouteKind })
        };
    }

    private static AcquisitionLocationTargetResolution ResolveCrop(
        AcquisitionRouteTargetDateUnlock route,
        AcquisitionLocationRouteSnapshotState state)
    {
        var capabilities = route.MatchingWindows
            .Select(window => window.RequiredLocationCapability ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (capabilities.Any(value => value.Length == 0))
        {
            return Exact(
                "native_season_crop_location",
                route.SourceId,
                "Farm",
                "static_calendar_resolution.routes[].crop_source.native_seasons");
        }
        if (!capabilities.SequenceEqual(new[] { "seeds_ignore_seasons" }))
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "crop_location_capability_not_supported" });
        }
        if (!state.LocationCapabilitiesComplete)
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "location_seeds_ignore_seasons_capability_missing" });
        }

        var targets = state.Locations
            .Where(location => location.SeedsIgnoreSeasonsHere == true)
            .Select(location => new AcquisitionLocationTarget(
                "runtime_seeds_ignore_seasons_location",
                route.SourceId,
                location.LocationId,
                new[]
                {
                    "state.locations.social_route_date_evidence.value.locations[].seeds_ignore_seasons_here"
                }))
            .OrderBy(target => target.LocationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new AcquisitionLocationTargetResolution(
            true,
            targets,
            targets.Length == 0
                ? new[] { "no_reachable_seeds_ignore_seasons_location_on_target_date" }
                : Array.Empty<string>());
    }

    private static AcquisitionLocationTargetResolution ResolveShop(
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionLocationRouteSnapshotState state)
    {
        var shop = staticRoute.ShopSource;
        if (shop is null || string.IsNullOrWhiteSpace(shop.ShopId))
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "authoritative_shop_source_binding_missing" });
        }
        if (state.RouteGraph.ValueKind != JsonValueKind.Object ||
            !state.RouteGraph.TryGetProperty("edges", out var edges) ||
            edges.ValueKind != JsonValueKind.Array)
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "runtime_shop_endpoint_route_graph_missing" });
        }

        var targets = new List<AcquisitionLocationTarget>();
        foreach (var edge in edges.EnumerateArray())
        {
            if (AcquisitionLocationRouteSnapshotState.ReadString(edge, "kind") !=
                    "shop_endpoint" ||
                !string.Equals(
                    AcquisitionLocationRouteSnapshotState.ReadString(
                        edge,
                        "shop_id"),
                    shop.ShopId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            var locationId = AcquisitionLocationRouteSnapshotState.ReadString(
                edge,
                "from_location");
            if (!string.IsNullOrWhiteSpace(locationId))
            {
                targets.Add(new AcquisitionLocationTarget(
                    "runtime_shop_endpoint_location",
                    shop.ShopId,
                    locationId,
                    new[]
                    {
                        "state.locations.route_graph.value.edges[kind=shop_endpoint]"
                    }));
            }
        }

        foreach (var endpoint in shop.InteractionEndpoints)
        {
            var normalized = endpoint.MapAsset.Replace('\\', '/');
            var locationId = normalized[(normalized.LastIndexOf('/') + 1)..];
            if (state.TryGetLocation(locationId, out _))
            {
                targets.Add(new AcquisitionLocationTarget(
                    "authoritative_shop_map_exact_runtime_location",
                    shop.ShopId,
                    locationId,
                    new[]
                    {
                        "static_calendar_resolution.routes[].shop_source.interaction_endpoints[]",
                        "state.locations.social_route_date_evidence.value.locations[].location_id"
                    }));
            }
        }

        var distinct = targets
            .DistinctBy(
                target => target.LocationId,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target.LocationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new AcquisitionLocationTargetResolution(
            true,
            distinct,
            distinct.Length == 0
                ? new[] { "shop_endpoint_location_not_present_on_target_date" }
                : Array.Empty<string>());
    }

    private static AcquisitionLocationTargetResolution ResolveCrabPot(
        AcquisitionRouteTargetDateUnlock route,
        AcquisitionLocationRouteSnapshotState state)
    {
        var network = state.CrabPotNetwork;
        if (network.ValueKind != JsonValueKind.Object ||
            AcquisitionLocationRouteSnapshotState.ReadString(
                network,
                "schema_version") != "crab_pot_network.v1" ||
            AcquisitionLocationRouteSnapshotState.ReadString(
                network,
                "projection_status") !=
                "complete_crab_pots_across_loaded_persistent_locations" ||
            !network.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "crab_pot_network_evidence_missing_or_incomplete" });
        }

        var targets = rows.EnumerateArray()
            .Where(row => RowContainsItem(row, route.QualifiedItemId))
            .Select(row => new AcquisitionLocationTarget(
                "runtime_crab_pot_production_location",
                route.SourceId,
                AcquisitionLocationRouteSnapshotState.ReadString(
                    row,
                    "location_id"),
                new[]
                {
                    "state.player.crab_pot_network.value.rows[].possible_qualified_item_ids"
                }))
            .Where(target => !string.IsNullOrWhiteSpace(target.LocationId))
            .DistinctBy(
                target => target.LocationId,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target.LocationId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new AcquisitionLocationTargetResolution(
            true,
            targets,
            targets.Length == 0
                ? new[] { "matching_crab_pot_runtime_location_not_present" }
                : Array.Empty<string>());
    }

    private static bool RowContainsItem(JsonElement row, string qualifiedItemId)
    {
        if (string.Equals(
                AcquisitionLocationRouteSnapshotState.ReadString(
                    row,
                    "current_output_qualified_item_id"),
                qualifiedItemId,
                StringComparison.Ordinal))
        {
            return true;
        }
        return row.TryGetProperty("possible_qualified_item_ids", out var possible) &&
            possible.ValueKind == JsonValueKind.Array &&
            possible.EnumerateArray().Any(value =>
                value.ValueKind == JsonValueKind.String &&
                string.Equals(
                    value.GetString(),
                    qualifiedItemId,
                    StringComparison.Ordinal));
    }

    private static AcquisitionLocationTargetResolution ResolveLocationSource(
        AcquisitionRouteTargetDateUnlock route,
        AcquisitionLocationRouteSnapshotState state)
    {
        var locationIds = route.MatchingWindows
            .Select(window => window.LocationId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (locationIds.Length != 1)
        {
            return new AcquisitionLocationTargetResolution(
                false,
                Array.Empty<AcquisitionLocationTarget>(),
                new[] { "authoritative_location_source_identity_ambiguous" });
        }

        var locationId = locationIds[0];
        if (locationId == "Default")
        {
            return Exact(
                "native_default_location_rule_current_location_witness",
                route.SourceId,
                state.CurrentLocationId,
                "native GameLocation default spawn rules plus state.player.location_id.value");
        }
        if (locationId.StartsWith("Farm_", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(state.FarmTypeKey))
            {
                return new AcquisitionLocationTargetResolution(
                    false,
                    Array.Empty<AcquisitionLocationTarget>(),
                    new[] { "farm_type_key_missing" });
            }
            var activeFarmDataKey = "Farm_" + state.FarmTypeKey;
            if (!string.Equals(
                    locationId,
                    activeFarmDataKey,
                    StringComparison.Ordinal))
            {
                return new AcquisitionLocationTargetResolution(
                    true,
                    Array.Empty<AcquisitionLocationTarget>(),
                    new[] { "location_rule_not_for_active_farm_type" });
            }
            return Exact(
                "native_active_farm_data_location",
                route.SourceId,
                "Farm",
                "state.farm.farm_type_key.value");
        }
        return Exact(
            "authoritative_location_data_source",
            route.SourceId,
            locationId,
            "static_calendar_resolution.routes[].matching_windows[].location_id");
    }

    private static AcquisitionLocationTargetResolution Exact(
        string bindingKind,
        string sourceKey,
        string locationId,
        string evidencePath) => new(
            true,
            new[]
            {
                new AcquisitionLocationTarget(
                    bindingKind,
                    sourceKey,
                    locationId,
                    new[] { evidencePath })
            },
            Array.Empty<string>());
}

internal sealed record AcquisitionLocationTargetResolution(
    bool EvidenceComplete,
    AcquisitionLocationTarget[] Targets,
    string[] Reasons);

internal sealed record AcquisitionLocationTarget(
    string BindingKind,
    string SourceKey,
    string LocationId,
    string[] EvidencePaths);
