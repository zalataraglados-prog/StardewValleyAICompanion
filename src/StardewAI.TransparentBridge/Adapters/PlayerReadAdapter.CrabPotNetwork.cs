using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadCrabPotNetwork(Farmer? player)
    {
        if (player is null || Game1.getFarm() is not { } farm)
        {
            return new
            {
                projection_status = "unavailable_world_player_or_farm",
                placed_pot_count = 0,
                rows = Array.Empty<object>()
            };
        }
        if (!SnapshotProfileContext.IncludesCrabPotNetwork)
        {
            return new
            {
                projection_status =
                    "blocked_requires_daily_fishing_or_full_profile",
                placed_pot_count = 0,
                rows = Array.Empty<object>()
            };
        }

        var locations = MachineLocationTopology.ReadPersistentLocations(
            farm,
            player);
        var fishData = DataLoader.Fish(Game1.content);
        var projections = locations
            .SelectMany(location => location.Location.objects.Pairs
                .Where(pair => pair.Value is CrabPot)
                .OrderBy(pair => pair.Key.Y)
                .ThenBy(pair => pair.Key.X)
                .Select(pair => ReadPlacedCrabPot(
                    location,
                    (int)pair.Key.X,
                    (int)pair.Key.Y,
                    (CrabPot)pair.Value,
                    player,
                    fishData)))
            .OrderBy(row => row.LocationId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.TileY)
            .ThenBy(row => row.TileX)
            .ToArray();

        return new
        {
            schema_version = "crab_pot_network.v1",
            projection_status =
                "complete_crab_pots_across_loaded_persistent_locations",
            location_count = locations.Length,
            placed_pot_count = projections.Length,
            exact_base_pot_count = projections.Count(row => row.ExactBase),
            unsupported_runtime_type_count = projections.Count(row => !row.ExactBase),
            production_domain_complete_count = projections.Count(row => row.ProductionDomainComplete),
            lifecycle_source =
                "live persistent GameLocation.objects; no save-history inference",
            runtime_rebind_policy =
                "route one edge then rebuild current-location exact collect or bait candidate",
            rows = projections.Select(row => row.Row).ToArray()
        };
    }

    private static PlacedCrabPotProjection ReadPlacedCrabPot(
        MachineLocationRef locationRef,
        int x,
        int y,
        CrabPot pot,
        Farmer currentPlayer,
        IDictionary<string, string> fishData)
    {
        var location = locationRef.Location;
        var exactBase = pot.GetType() == typeof(CrabPot);
        var resolvedOwner = Game1.GetPlayer(pot.owner.Value);
        var owner = resolvedOwner ?? currentPlayer;
        var production = exactBase
            ? ReadCrabPotTileProductionContext(
                location,
                x,
                y,
                owner,
                fishData)
            : null;
        var possible = production?.CatchRows
            .Select(row => row.QualifiedItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        var output = pot.heldObject.Value;
        var ownerHasLuremaster = owner.professions.Contains(11);
        var readyStateConsistent = pot.readyForHarvest.Value ==
            (pot.tileIndexToShow == 714 && output is not null);
        var serviceStatus = !exactBase
            ? "unsupported_runtime_type"
            : !readyStateConsistent
                ? "blocked_inconsistent_ready_state"
                : pot.readyForHarvest.Value
                    ? "ready_for_collection"
                    : pot.bait.Value is null && !ownerHasLuremaster
                        ? "bait_required"
                        : "producing_or_waiting";
        var productionComplete = exactBase &&
            production is not null &&
            !string.IsNullOrWhiteSpace(production.Signature) &&
            possible.Length > 0;
        var row = new
        {
            location_id = location.NameOrUniqueName,
            location_name = location.Name,
            location_kind = locationRef.Kind,
            root_location_id = locationRef.RootLocationId,
            location_is_current = ReferenceEquals(location, Game1.currentLocation),
            tile_x = x,
            tile_y = y,
            qualified_item_id = pot.QualifiedItemId,
            runtime_type = pot.GetType().FullName,
            exact_base_crab_pot = exactBase,
            owner_player_id = pot.owner.Value,
            owner_resolution_status = resolvedOwner is null
                ? "fallback_current_player"
                : "resolved_native_player",
            owner_has_mariner = owner.professions.Contains(10),
            owner_has_luremaster = ownerHasLuremaster,
            service_status = serviceStatus,
            tile_index = pot.tileIndexToShow,
            ready_for_harvest = pot.readyForHarvest.Value,
            ready_state_consistent = readyStateConsistent,
            bait_qualified_item_id = pot.bait.Value?.QualifiedItemId ?? string.Empty,
            current_output_qualified_item_id = output?.QualifiedItemId ?? string.Empty,
            current_output_stack = output?.Stack ?? 0,
            current_output_quality = output?.Quality ?? 0,
            current_output_collection_eligible =
                CrabPotProjectionSemantics.IsFishCollectionEligible(
                    output,
                    fishData),
            production_signature = production?.Signature ?? string.Empty,
            fish_area_id = production?.FishAreaId ?? string.Empty,
            fish_area_display_name =
                production?.FishAreaDisplayName ?? string.Empty,
            habitat_tags = production?.HabitatTags ?? Array.Empty<string>(),
            native_order_catch_rows = production?.CatchRows ??
                Array.Empty<CrabPotCatchRow>(),
            conservative_serviced_probability_status =
                production?.ConservativeServicedProbabilityStatus ??
                "unavailable_production_context",
            conservative_serviced_outcome_rows =
                production?.ConservativeServicedOutcomeRows ??
                Array.Empty<CrabPotServicedOutcomeRow>(),
            conservative_serviced_trash_probability =
                production?.ConservativeServicedTrashProbability,
            possible_qualified_item_ids = possible,
            production_domain_complete = productionComplete
        };
        return new PlacedCrabPotProjection(
            location.NameOrUniqueName,
            x,
            y,
            exactBase,
            productionComplete,
            row);
    }

    private sealed record PlacedCrabPotProjection(
        string LocationId,
        int TileX,
        int TileY,
        bool ExactBase,
        bool ProductionDomainComplete,
        object Row);
}
