namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    private static readonly HashSet<string> CandidateDeclaredSourceRouteKinds =
        new(StringComparer.Ordinal)
        {
            "creates_reward_item",
            "machine_output",
            "native_farm_animal_deluxe_produce",
            "native_farm_animal_produce",
            "native_fish_pond_output",
            "native_fruit_tree_produce",
            "native_geode_default_drop",
            "native_geode_drop",
            "native_location_artifact_spot",
            "native_location_forage_spawn",
            "native_machine_flavored_output",
            "native_machine_item_query_output",
            "native_mine_buried_item",
            "native_money_payment",
            "native_monster_drop_table",
            "native_object_artifact_spot_chance",
            "native_radioactive_ore_node",
            "native_solar_panel_output",
            "native_wild_tree_chop_drop",
            "native_wild_tree_seed",
            "native_wild_tree_seed_drop",
            "native_wild_tree_tapper_output",
            "recipe_output"
        };

    internal static AcquisitionRouteSourceIdentityContract?
        DescribeAuthoritativeSourceContract(string routeKind)
    {
        if (routeKind == "sells")
        {
            return Contract(
                routeKind,
                "shop_id",
                "candidate.shop_id");
        }
        if (routeKind == "harvests_as")
        {
            return Contract(
                routeKind,
                "harvest_source_seed_id",
                "candidate.harvest_source_seed_id");
        }
        if (routeKind == "native_location_fish_spawn")
        {
            return Contract(
                routeKind,
                "complete_runtime_fishing_outcome_source",
                "candidate.outcome_distribution_complete",
                "candidate.outcome_distribution_json");
        }
        if (routeKind == "native_mine_fishing_override")
        {
            return Contract(
                routeKind,
                "validated_master_angler_mine_override",
                "validated_master_angler_mine_override");
        }
        if (routeKind == "native_crab_pot_output")
        {
            return Contract(
                routeKind,
                "exact_crab_pot_output_identity",
                "candidate.kind",
                "qualified_item_id");
        }
        if (FixedNativeSources.TryGetValue(routeKind, out var fixedSource))
        {
            return new AcquisitionRouteSourceIdentityContract(
                routeKind,
                "fixed_decompiled_native_source",
                new[] { "candidate.kind", "fixed.source_id" },
                fixedSource.SourceId,
                fixedSource.CandidateKinds.ToArray());
        }
        return CandidateDeclaredSourceRouteKinds.Contains(routeKind)
            ? Contract(
                routeKind,
                "typed_authoritative_route_sources_json",
                "candidate.authoritative_route_sources_json")
            : null;
    }

    internal static bool CanRepresentAuthoritativeSource(
        string routeKind,
        string sourceId,
        string qualifiedItemId,
        out AcquisitionRouteSourceIdentityContract? contract,
        out string reason)
    {
        contract = DescribeAuthoritativeSourceContract(routeKind);
        reason = string.Empty;
        if (contract is null)
        {
            reason = "unsupported_source_identity_route_kind";
            return false;
        }
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            reason = "empty_authoritative_source_id";
            return false;
        }

        var sourceShapeValid = contract.EvidenceMode switch
        {
            "shop_id" => sourceId.StartsWith("shop:", StringComparison.Ordinal) &&
                sourceId.Length > "shop:".Length,
            "harvest_source_seed_id" =>
                sourceId.StartsWith("crop:", StringComparison.Ordinal) &&
                sourceId.Length > "crop:".Length,
            "complete_runtime_fishing_outcome_source" =>
                sourceId.StartsWith("location_fish:", StringComparison.Ordinal) &&
                sourceId.Length > "location_fish:".Length,
            "validated_master_angler_mine_override" => true,
            "exact_crab_pot_output_identity" =>
                sourceId == "crab_pot_fish:" + ItemId(qualifiedItemId),
            "fixed_decompiled_native_source" =>
                sourceId == contract.FixedSourceId,
            "typed_authoritative_route_sources_json" => true,
            _ => false
        };
        if (!sourceShapeValid)
        {
            reason = "authoritative_source_shape_mismatch";
            return false;
        }
        if (routeKind != "native_money_payment" &&
            string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            reason = "empty_authoritative_qualified_item_id";
            return false;
        }
        return true;
    }

    private static AcquisitionRouteSourceIdentityContract Contract(
        string routeKind,
        string evidenceMode,
        params string[] evidenceFields) => new(
            routeKind,
            evidenceMode,
            evidenceFields,
            string.Empty,
            Array.Empty<string>());
}

internal sealed record AcquisitionRouteSourceIdentityContract(
    string RouteKind,
    string EvidenceMode,
    string[] EvidenceFields,
    string FixedSourceId,
    string[] CandidateKinds);
