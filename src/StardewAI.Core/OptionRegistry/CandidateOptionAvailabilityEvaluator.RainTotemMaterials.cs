using System;
using System.Collections.Generic;
using System.Globalization;
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
    private static readonly IReadOnlyDictionary<string, int>
        RainTotemIngredientAmounts = new Dictionary<string, int>(
            StringComparer.Ordinal)
        {
            ["(O)709"] = 1,
            ["(O)432"] = 1,
            ["(O)726"] = 5
        };

    private EventCandidate[] RainTotemImmediateMaterialSupplyCandidates(
        SnapshotEnvelope snapshot,
        JsonElement recipeRow,
        MasterAnglerRainTotemSupplyIntentValidation intent)
    {
        if (!TryReadRainTotemMaterialDeficits(
                recipeRow,
                out var deficits) ||
            deficits.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var machineCandidates = MachineServiceCandidates(snapshot, null);
        var machineOutputs = machineCandidates
            .Where(candidate => candidate.Available &&
                candidate.Kind is "collect_machine_output_tile" or
                    "route_connector_tile")
            .ToArray();
        var machineInputs = machineCandidates
            .Where(candidate => candidate.Available &&
                string.Equals(
                    candidate.Kind,
                    "load_machine_input_tile",
                    StringComparison.Ordinal))
            .ToArray();
        var spawnedObjects = SpawnedObjectForagingCandidates(snapshot)
            .Where(candidate => candidate.Available)
            .ToArray();
        var wildTrees = WildTreeChopAcquisitionCandidates(snapshot)
            .Where(candidate => candidate.Available)
            .ToArray();
        var farmResourceClumps = ClearFarmResourceClumpCandidates(snapshot)
            .Where(candidate => candidate.Available)
            .ToArray();

        var candidates = new List<EventCandidate>();
        foreach (var deficit in deficits)
        {
            candidates.AddRange(machineOutputs
                .Where(candidate => string.Equals(
                    candidate.QualifiedItemId,
                    deficit.QualifiedItemId,
                    StringComparison.Ordinal))
                .Select(candidate => BindRainTotemMaterialSupplyCandidate(
                    candidate,
                    deficit,
                    intent,
                    "collect_ready_native_machine_output")));

            if (string.Equals(
                    deficit.QualifiedItemId,
                    "(O)709",
                    StringComparison.Ordinal))
            {
                candidates.AddRange(wildTrees
                    .Where(candidate => CandidateGuaranteesItem(
                        candidate,
                        deficit.QualifiedItemId))
                    .Select(candidate => BindRainTotemMaterialSupplyCandidate(
                        candidate,
                        deficit,
                        intent,
                        "chop_exact_guaranteed_hardwood_tree")));
                candidates.AddRange(farmResourceClumps
                    .Where(candidate => CandidateGuaranteesItem(
                        candidate,
                        deficit.QualifiedItemId))
                    .Select(candidate => BindRainTotemMaterialSupplyCandidate(
                        candidate,
                        deficit,
                        intent,
                        "clear_exact_farm_resource_clump_for_hardwood")));
            }

            if (string.Equals(
                    deficit.QualifiedItemId,
                    "(O)432",
                    StringComparison.Ordinal))
            {
                candidates.AddRange(machineInputs
                    .Where(candidate => string.Equals(
                            candidate.QualifiedItemId,
                            "(O)430",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadParameter(
                                candidate.Parameters,
                                "predicted_output_qualified_item_id"),
                            "(O)432",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadParameter(
                                candidate.Parameters,
                                "machine_qualified_item_id"),
                            "(BC)19",
                            StringComparison.Ordinal))
                    .Select(candidate => BindRainTotemMaterialSupplyCandidate(
                        candidate,
                        deficit,
                        intent,
                        "load_native_oil_maker_with_truffle")));
                candidates.AddRange(spawnedObjects
                    .Where(candidate => string.Equals(
                        candidate.QualifiedItemId,
                        "(O)430",
                        StringComparison.Ordinal))
                    .Select(candidate => BindRainTotemMaterialSupplyCandidate(
                        candidate,
                        deficit,
                        intent,
                        "collect_native_pig_truffle_input")));
            }
        }

        return candidates
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EventCandidate BindRainTotemMaterialSupplyCandidate(
        EventCandidate candidate,
        RainTotemMaterialDeficit deficit,
        MasterAnglerRainTotemSupplyIntentValidation intent,
        string producerStage)
    {
        var parameters = candidate.Parameters
            .Concat(CanonicalRainTotemSupplyParameters(intent))
            .Concat(new[]
            {
                Parameter(
                    "rain_totem_material_supply_status",
                    "exact_immediate_native_producer_bound"),
                Parameter(
                    "rain_totem_material_target_qualified_item_id",
                    deficit.QualifiedItemId),
                Parameter(
                    "rain_totem_material_required_count",
                    deficit.RequiredCount.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "rain_totem_material_available_count",
                    deficit.AvailableCount.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "rain_totem_material_deficit_count",
                    deficit.DeficitCount.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "rain_totem_material_producer_stage",
                    producerStage),
                Parameter(
                    "rain_totem_material_fresh_replan_required",
                    "true")
            })
            .ToArray();
        return CloneCandidate(
            candidate,
            candidateId: "rain-totem-material:" +
                deficit.QualifiedItemId + ":" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";rain_totem_material_target_qualified_item_id=" +
                deficit.QualifiedItemId +
                ";rain_totem_material_deficit_count=" +
                deficit.DeficitCount.ToString(CultureInfo.InvariantCulture) +
                ";rain_totem_material_producer_stage=" + producerStage +
                ";fresh_snapshot_and_weather_supply_replan_required=true",
            parameters: parameters,
            availabilityClass:
                "master_angler_rain_totem_material_supply_" + producerStage);
    }

    private static bool TryReadRainTotemMaterialDeficits(
        JsonElement recipeRow,
        out RainTotemMaterialDeficit[] deficits)
    {
        deficits = Array.Empty<RainTotemMaterialDeficit>();
        var sources = new List<JsonElement> { recipeRow };
        if (recipeRow.TryGetProperty(
                "workbench_crafting_sources",
                out var workbenches) &&
            workbenches.ValueKind == JsonValueKind.Array)
        {
            sources.AddRange(workbenches.EnumerateArray().Where(source =>
                source.ValueKind == JsonValueKind.Object));
        }

        var projections = sources
            .Select(TryReadRainTotemMaterialDeficitsFromSource)
            .Where(result => result is not null)
            .Select(result => result!)
            .OrderBy(result => result.Sum(row => row.DeficitCount))
            .ThenBy(result => string.Join(
                "|",
                result.Select(row => row.QualifiedItemId)),
                StringComparer.Ordinal)
            .ToArray();
        if (projections.Length == 0)
            return false;

        deficits = projections[0]
            .Where(row => row.DeficitCount > 0)
            .ToArray();
        return true;
    }

    private static RainTotemMaterialDeficit[]?
        TryReadRainTotemMaterialDeficitsFromSource(JsonElement source)
    {
        if (!source.TryGetProperty("ingredient_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            rows.GetArrayLength() != RainTotemIngredientAmounts.Count)
        {
            return null;
        }

        var result = new List<RainTotemMaterialDeficit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            var rawId = ReadString(row, "requirement_id_or_category");
            var qualifiedItemId = rawId.StartsWith("(O)", StringComparison.Ordinal)
                ? rawId
                : "(O)" + rawId;
            if (!RainTotemIngredientAmounts.TryGetValue(
                    qualifiedItemId,
                    out var exactRequired) ||
                !seen.Add(qualifiedItemId))
            {
                return null;
            }
            var required = ReadInt(row, "required_count");
            var available = Math.Max(
                0,
                ReadInt(row, "available_count_before_this_ingredient"));
            if (required != exactRequired)
                return null;
            result.Add(new RainTotemMaterialDeficit(
                qualifiedItemId,
                required,
                available,
                Math.Max(0, required - available)));
        }
        return seen.SetEquals(RainTotemIngredientAmounts.Keys)
            ? result.OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
                .ToArray()
            : null;
    }

    private static bool CandidateGuaranteesItem(
        EventCandidate candidate,
        string qualifiedItemId) =>
        CandidateGuaranteedItemQuantity(candidate, qualifiedItemId) > 0;

    private static int CandidateGuaranteedItemQuantity(
        EventCandidate candidate,
        string qualifiedItemId)
    {
        var json = ReadParameter(
            candidate.Parameters,
            "tree_chop_guaranteed_minimum_outputs_json");
        if (string.IsNullOrWhiteSpace(json))
        {
            json = ReadParameter(
                candidate.Parameters,
                "expected_output_items_json");
        }
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        return CandidateJsonItemQuantity(json, qualifiedItemId);
    }

    private static int CandidateJsonItemQuantity(
        string json,
        string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray()
                    .Where(row =>
                    string.Equals(
                        ReadStringEither(
                            row,
                            "qualified_item_id",
                            "qualifiedItemId"),
                        qualifiedItemId,
                        StringComparison.Ordinal))
                    .Sum(row => Math.Max(
                        0,
                        ReadIntEither(row, "quantity_min", "quantityMin") is > 0
                            ? ReadIntEither(row, "quantity_min", "quantityMin")
                            : ReadIntEither(row, "quantity", "Quantity")))
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string ReadStringEither(
        JsonElement source,
        string first,
        string second) =>
        ReadString(source, first) is { Length: > 0 } value
            ? value
            : ReadString(source, second);

    private static int ReadIntEither(
        JsonElement source,
        string first,
        string second)
    {
        var value = ReadInt(source, first);
        return value != 0 ? value : ReadInt(source, second);
    }

    private sealed record RainTotemMaterialDeficit(
        string QualifiedItemId,
        int RequiredCount,
        int AvailableCount,
        int DeficitCount);
}
