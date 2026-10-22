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
        if (!TrySelectCrabPotInfrastructureNeed(snapshot, out var need))
        {
            return Array.Empty<EventCandidate>();
        }

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

        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "output_qualified_item_id"),
                    "(O)710",
                    StringComparison.Ordinal))
            .SelectMany(row => BuildCrabPotCraftingCandidates(
                snapshot,
                row,
                need.LocationId,
                need.ProductionSource,
                need.PossibleSpecies,
                need.TargetSpecies,
                need.NetworkRows))
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
        foreach (var binding in ExactSupportCraftingCandidateBindings(
                     snapshot,
                     row,
                     "crab_pot"))
        {
            yield return BuildCrabPotCraftingCandidate(
                snapshot,
                binding,
                placementLocationId,
                productionSource,
                possibleSpecies,
                targetSpecies,
                networkRows);
        }
    }

    private EventCandidate BuildCrabPotCraftingCandidate(
        SnapshotEnvelope snapshot,
        ExactSupportCraftingCandidateBinding binding,
        string placementLocationId,
        JsonElement productionSource,
        string[] possibleSpecies,
        string[] targetSpecies,
        JsonElement[] networkRows)
    {
        var reasons = new List<string>(binding.BlockingReasons);
        var parameters = binding.Parameters.Concat(new[]
        {
            Parameter("crab_pot_crafting_reason", "master_angler_missing_trap_species_capacity"),
            Parameter("crab_pot_placement_location_id", placementLocationId),
            Parameter("crab_pot_production_signature", ReadString(productionSource, "production_signature")),
            Parameter("possible_qualified_item_ids_json", JsonSerializer.Serialize(possibleSpecies)),
            Parameter("master_angler_target_qualified_item_ids_json", JsonSerializer.Serialize(targetSpecies)),
            Parameter("crab_pot_capacity_assessment_json", CrabPotCapacityEvidenceJson(snapshot, networkRows, productionSource, targetSpecies))
        }).ToArray();
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
                ReadString(binding.RecipeRow, "recipe_name") +
                (binding.UsesWorkbench
                    ? ":workbench:" + ReadString(
                        binding.Source,
                        "workbench_access_point_id")
                    : ":personal"),
            Kind = "craft_crab_pot",
            Available = reasons.Count == 0,
            LocationId = binding.LocationId,
            ItemId = ReadString(binding.RecipeRow, "output_item_id"),
            QualifiedItemId = "(O)710",
            Quantity = binding.OutputCount,
            EstimatedTicks = 30,
            EnergyCost = 0,
            AvailabilityClass = binding.UsesWorkbench
                ? "transparent_crab_pot_native_workbench_crafting"
                : "transparent_crab_pot_native_personal_crafting",
            ExpectedEffect =
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=(O)710:" +
                binding.OutputCount +
                ";crab_pot_lifecycle_replan_for_placement=true",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }
}
