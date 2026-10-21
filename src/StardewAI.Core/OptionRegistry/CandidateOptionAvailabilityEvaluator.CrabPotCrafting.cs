using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CrabPotCraftingCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) ||
            missing.Count == 0 ||
            !TryReadCompleteCrabPotNetwork(snapshot, out var networkRows))
        {
            return Array.Empty<EventCandidate>();
        }

        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            ReadInt(placement.Value, "inventory_crab_pot_count") > 0 ||
            !placement.Value.TryGetProperty(
                "potential_placement_locations",
                out var locations) ||
            locations.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var usefulSources = locations.EnumerateArray()
            .Where(location => location.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(location, "placement_probe_status"),
                    "native_legal_water_tiles_available",
                    StringComparison.Ordinal) &&
                location.TryGetProperty(
                    "static_legal_tile_ranges",
                    out var ranges) &&
                ranges.ValueKind == JsonValueKind.Array)
            .SelectMany(location => location
                .GetProperty("static_legal_tile_ranges")
                .EnumerateArray()
                .Select(range => new
                {
                    LocationId = ReadString(location, "location_id"),
                    Range = range,
                    Possible = CrabPotRangePossibleSpecies(range)
                }))
            .Select(entry => new
            {
                entry.LocationId,
                entry.Range,
                entry.Possible,
                Targets = entry.Possible
                    .Where(missing.Contains)
                    .Where(value => CrabPotAdditionalCapacityCanAdvance(
                        snapshot,
                        networkRows,
                        entry.Range,
                        value))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            })
            .Where(entry => entry.Targets.Length > 0)
            .OrderBy(entry => entry.LocationId, StringComparer.Ordinal)
            .ThenBy(entry => ReadInt(entry.Range, "y"))
            .ThenBy(entry => ReadInt(entry.Range, "start_x"))
            .ToArray();
        if (usefulSources.Length == 0)
            return Array.Empty<EventCandidate>();

        var crafting = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_crafting");
        if (!crafting.HasValue ||
            crafting.Value.ValueKind != JsonValueKind.Object ||
            !crafting.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var source = usefulSources[0];
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "output_qualified_item_id"),
                    "(O)710",
                    StringComparison.Ordinal))
            .SelectMany(row => BuildCrabPotCraftingCandidates(
                snapshot,
                row,
                source.LocationId,
                source.Range,
                source.Possible,
                source.Targets,
                networkRows))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<EventCandidate> BuildCrabPotCraftingCandidates(
        SnapshotEnvelope snapshot,
        JsonElement row,
        string placementLocationId,
        JsonElement productionSource,
        string[] possibleSpecies,
        string[] targetSpecies,
        JsonElement[] networkRows)
    {
        yield return BuildCrabPotCraftingCandidate(
            snapshot,
            row,
            null,
            placementLocationId,
            productionSource,
            possibleSpecies,
            targetSpecies,
            networkRows);
        if (!row.TryGetProperty(
                "workbench_crafting_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }
        foreach (var source in sources.EnumerateArray().Where(value =>
                     value.ValueKind == JsonValueKind.Object))
        {
            yield return BuildCrabPotCraftingCandidate(
                snapshot,
                row,
                source,
                placementLocationId,
                productionSource,
                possibleSpecies,
                targetSpecies,
                networkRows);
        }
    }

    private EventCandidate BuildCrabPotCraftingCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement? workbenchSource,
        string placementLocationId,
        JsonElement productionSource,
        string[] possibleSpecies,
        string[] targetSpecies,
        JsonElement[] networkRows)
    {
        var usesWorkbench = workbenchSource.HasValue;
        var source = workbenchSource ?? row;
        var locationId = usesWorkbench
            ? ReadString(source, "location_id")
            : ReadStateFieldString(snapshot, "player", "location_id");
        var targetX = usesWorkbench ? NullableReadInt(source, "tile_x") : null;
        var targetY = usesWorkbench ? NullableReadInt(source, "tile_y") : null;
        var sameLocation = string.Equals(
            locationId,
            ReadStateFieldString(snapshot, "player", "location_id"),
            StringComparison.Ordinal);
        var stand = usesWorkbench && sameLocation &&
            targetX.HasValue && targetY.HasValue
                ? FindBestStandTile(snapshot, targetX.Value, targetY.Value)
                : null;
        var ingredients = source.TryGetProperty(
                "ingredient_rows",
                out var ingredientRows)
            ? ingredientRows
            : default;
        var ingredientRowsJson = ingredients.ValueKind == JsonValueKind.Array
            ? ingredients.GetRawText()
            : "[]";
        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(snapshot, ingredients, usesWorkbench, null);
        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var reasons = new List<string>();
        if (!string.Equals(
                ReadString(source, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal))
        {
            reasons.Add("crab_pot_recipe_not_ready");
        }
        if (ReadBool(
                source,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add("crab_pot_output_cannot_fit_after_material_consumption");
        }
        if (ActiveMenuOpenForCandidate(snapshot))
            reasons.Add("crab_pot_crafting_menu_must_be_clear");
        if (usesWorkbench && (!sameLocation || stand is null))
        {
            reasons.Add(sameLocation
                ? "crab_pot_workbench_access_unavailable"
                : "crab_pot_workbench_requires_current_location_rebind");
        }
        if (!reservation.Ready)
            reasons.AddRange(reservation.BlockingReasons);

        var nodeIdsJson = usesWorkbench && source.TryGetProperty(
                "native_container_node_ids",
                out var nodeIds)
            ? nodeIds.GetRawText()
            : "[]";
        var parameters = new[]
        {
            Parameter("recipe_name", ReadString(row, "recipe_name")),
            Parameter("output_qualified_item_id", "(O)710"),
            Parameter("output_item_id", ReadString(row, "output_item_id")),
            Parameter("output_count", Math.Max(1, ReadInt(row, "output_count_per_craft", 1)).ToString()),
            Parameter("times_crafted_before", Math.Max(0, ReadInt(row, "times_crafted")).ToString()),
            Parameter("ingredient_rows_json", ingredientRowsJson),
            Parameter("crafting_source", usesWorkbench
                ? "native_workbench_crafting_menu"
                : "native_personal_crafting_menu"),
            Parameter("workbench_access_point_id", usesWorkbench
                ? ReadString(source, "workbench_access_point_id")
                : string.Empty),
            Parameter("workbench_container_node_ids_json", nodeIdsJson),
            Parameter("location_id", locationId),
            Parameter("target_tile_x", targetX?.ToString()),
            Parameter("target_tile_y", targetY?.ToString()),
            Parameter("stand_tile_x", stand?.X.ToString()),
            Parameter("stand_tile_y", stand?.Y.ToString()),
            Parameter("crab_pot_crafting_reason", "master_angler_missing_trap_species_capacity"),
            Parameter("crab_pot_placement_location_id", placementLocationId),
            Parameter("crab_pot_production_signature", ReadString(productionSource, "production_signature")),
            Parameter("possible_qualified_item_ids_json", JsonSerializer.Serialize(possibleSpecies)),
            Parameter("master_angler_target_qualified_item_ids_json", JsonSerializer.Serialize(targetSpecies)),
            Parameter("crab_pot_capacity_assessment_json", CrabPotCapacityEvidenceJson(snapshot, networkRows, productionSource, targetSpecies)),
            Parameter("commitment_ledger_id", reservation.LedgerId),
            Parameter("commitment_ledger_revision", reservation.LedgerRevision.ToString()),
            Parameter("material_reservation_guard_status", reservation.Status),
            Parameter("material_reservation_ledger_id", reservation.LedgerId),
            Parameter("material_reservation_ledger_revision", reservation.LedgerRevision.ToString()),
            Parameter("material_reservation_ids_json", JsonSerializer.Serialize(reservation.ReservationIds))
        };
        reasons.AddRange(CompilerProbeBlockingReasons(
            snapshot,
            new OptionAvailabilityCandidate
            {
                OptionId = "executor.craft_crab_pot",
                Parameters = parameters
            }));

        return new EventCandidate
        {
            CandidateId = "craft-crab-pot:" +
                ReadString(row, "recipe_name") +
                (usesWorkbench
                    ? ":workbench:" + ReadString(
                        source,
                        "workbench_access_point_id")
                    : ":personal"),
            Kind = "craft_crab_pot",
            Available = reasons.Count == 0,
            LocationId = locationId,
            ItemId = ReadString(row, "output_item_id"),
            QualifiedItemId = "(O)710",
            Quantity = Math.Max(1, ReadInt(row, "output_count_per_craft", 1)),
            EstimatedTicks = 30,
            EnergyCost = 0,
            AvailabilityClass = usesWorkbench
                ? "transparent_crab_pot_native_workbench_crafting"
                : "transparent_crab_pot_native_personal_crafting",
            ExpectedEffect =
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=(O)710:" +
                Math.Max(1, ReadInt(row, "output_count_per_craft", 1)) +
                ";crab_pot_lifecycle_replan_for_placement=true",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }
}
