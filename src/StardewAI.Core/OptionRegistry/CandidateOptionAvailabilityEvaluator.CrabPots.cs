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
    private EventCandidate[] CrabPotCollectCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters)
    {
        var candidates = CrabPotLifecycleCandidates(snapshot);
        if (!HasMasterAnglerIntentParameters(boundParameters))
            return candidates;

        if (!TryNormalizeMasterAnglerIntent(
                snapshot,
                boundParameters,
                out var intentParameters,
                out var normalizationReason))
        {
            return new[]
            {
                BlockedMasterAnglerCrabPotCandidate(
                    normalizationReason,
                    intentParameters)
            };
        }
        if (!MasterAnglerWindowIntentValidator.TryValidate(
                snapshot,
                intentParameters,
                out var intent,
                out var validationReason))
        {
            return new[]
            {
                BlockedMasterAnglerCrabPotCandidate(
                    validationReason,
                    intentParameters)
            };
        }
        if (!string.Equals(intent.SourceKind, "crab_pot", StringComparison.Ordinal))
        {
            return new[]
            {
                BlockedMasterAnglerCrabPotCandidate(
                    "master_angler_crab_pot_source_kind_mismatch",
                    intentParameters)
            };
        }

        var continuation = MasterAnglerContinuationParameters(
            "fishing.collect_crab_pots",
            intentParameters);
        var exact = candidates
            .Where(candidate => candidate.Available &&
                string.Equals(candidate.Kind, "collect_crab_pot", StringComparison.Ordinal) &&
                string.Equals(
                    candidate.QualifiedItemId,
                    intent.TargetQualifiedItemId,
                    StringComparison.Ordinal) &&
                candidate.Parameters.Any(parameter =>
                    parameter.Name == "expected_fish_collection_eligible" &&
                    parameter.Value == "1"))
            .Select(candidate => ApplyMasterAnglerTimeBudget(
                snapshot,
                CloneCandidate(
                    candidate,
                    candidateId: candidate.CandidateId +
                        ":master-angler:" + intent.TargetQualifiedItemId,
                    expectedEffect: candidate.ExpectedEffect +
                        ";master_angler_target_qualified_item_id=" +
                        intent.TargetQualifiedItemId +
                        ";master_angler_runtime_terminal_validation_required=true",
                    parameters: candidate.Parameters
                        .Concat(intentParameters)
                        .Concat(continuation)
                        .ToArray(),
                    availabilityClass: "master_angler_exact_ready_crab_pot"),
                intent,
                terminalReserveTicks: 0))
            .ToArray();
        return exact.Length > 0
            ? exact
            : new[]
            {
                BlockedMasterAnglerCrabPotCandidate(
                    "master_angler_no_exact_ready_crab_pot_output",
                    intentParameters)
            };
    }

    private static string[] CrabPotRangePossibleSpecies(JsonElement range)
    {
        if (range.ValueKind != JsonValueKind.Object ||
            !range.TryGetProperty(
                "native_order_catch_rows",
                out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        return rows.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select(value => ReadString(value, "qualified_item_id"))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static SmallModelActionParameter[] CrabPotParameters(
        JsonElement item,
        int x,
        int y,
        int standX,
        int standY,
        string outputQualifiedItemId,
        string outputItemsJson)
    {
        var productionPossible = ReadCrabPotStringArray(
            item,
            "crab_pot_possible_qualified_item_ids");
        return new[]
        {
            Parameter("target_tile_x", x.ToString()),
            Parameter("target_tile_y", y.ToString()),
            Parameter("stand_tile_x", standX.ToString()),
            Parameter("stand_tile_y", standY.ToString()),
            Parameter("target_runtime_type", ReadString(item, "type")),
            Parameter("qualified_item_id", outputQualifiedItemId),
            Parameter("quantity", ReadInt(item, "crab_pot_output_stack_on_collect").ToString()),
            Parameter("expected_output_items_json", outputItemsJson),
            Parameter("expected_output_state_context", ReadString(item, "crab_pot_output_state_context")),
            Parameter("book_double_roll_succeeded", BoolInt(item, "crab_pot_book_double_roll_succeeded")),
            Parameter("book_crabbing_owned", BoolInt(item, "crab_pot_book_crabbing_owned")),
            Parameter("book_double_applied", BoolInt(item, "crab_pot_book_double_applied")),
            Parameter("expected_skill_id", "fishing"),
            Parameter("expected_skill_experience_delta", ReadInt(item, "crab_pot_fishing_experience_on_success_min").ToString()),
            Parameter("expected_container_bait_qualified_item_id", ReadString(item, "crab_pot_bait_qualified_item_id")),
            Parameter("expected_fish_collection_eligible", BoolInt(item, "crab_pot_fish_collection_eligible")),
            Parameter("expected_fish_caught_count_before", ReadInt(item, "crab_pot_fish_caught_count_before").ToString()),
            Parameter("expected_fish_caught_count_after", ReadInt(item, "crab_pot_fish_caught_count_after").ToString()),
            Parameter("expected_fish_caught_max_size_before", ReadInt(item, "crab_pot_fish_caught_max_size_before").ToString()),
            Parameter("expected_catch_size_min", ReadInt(item, "crab_pot_catch_size_min").ToString()),
            Parameter("expected_catch_size_max", ReadInt(item, "crab_pot_catch_size_max").ToString()),
            Parameter("catch_size_projection_status", ReadString(item, "crab_pot_catch_size_projection_status")),
            Parameter(
                "crab_pot_production_signature",
                ReadString(item, "crab_pot_production_signature")),
            Parameter(
                "crab_pot_production_possible_qualified_item_ids_json",
                JsonSerializer.Serialize(productionPossible)),
            Parameter(
                "crab_pot_production_domain_complete",
                productionPossible.Length > 0 &&
                !string.IsNullOrWhiteSpace(
                    ReadString(item, "crab_pot_production_signature"))
                    ? "true"
                    : "false"),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string CrabPotExpectedEffect(JsonElement item, CandidateTile? stand, string outputQualifiedItemId, string outputItemsJson)
    {
        return (stand is not null ? "crab_pot_stand_tile=" + stand.X + "," + stand.Y + ";" : string.Empty) +
            "crab_pot_ready_for_harvest=false" +
            ";crab_pot_bait_qualified_item_id=" + ReadString(item, "crab_pot_bait_qualified_item_id") +
            ";qualified_item_id=" + outputQualifiedItemId +
            ";quantity=" + ReadInt(item, "crab_pot_output_stack_on_collect") +
            ";expected_output_items_json=" + outputItemsJson +
            ";expected_output_state_context=" + ReadString(item, "crab_pot_output_state_context") +
            ";book_double_roll_succeeded=" + BoolInt(item, "crab_pot_book_double_roll_succeeded") +
            ";book_crabbing_owned=" + BoolInt(item, "crab_pot_book_crabbing_owned") +
            ";book_double_applied=" + BoolInt(item, "crab_pot_book_double_applied") +
            ";expected_skill_id=fishing" +
            ";expected_skill_experience_delta=" + ReadInt(item, "crab_pot_fishing_experience_on_success_min") +
            ";expected_fish_collection_eligible=" + BoolInt(item, "crab_pot_fish_collection_eligible") +
            ";expected_fish_caught_count_before=" + ReadInt(item, "crab_pot_fish_caught_count_before") +
            ";expected_fish_caught_count_after=" + ReadInt(item, "crab_pot_fish_caught_count_after") +
            ";expected_fish_caught_max_size_before=" + ReadInt(item, "crab_pot_fish_caught_max_size_before") +
            ";expected_catch_size_min=" + ReadInt(item, "crab_pot_catch_size_min") +
            ";expected_catch_size_max=" + ReadInt(item, "crab_pot_catch_size_max") +
            ";catch_size_projection_status=" + ReadString(item, "crab_pot_catch_size_projection_status") +
            ";max_movement_tiles=512";
    }

    private static string BoolInt(JsonElement item, string property)
    {
        return ReadBool(item, property) == true ? "1" : "0";
    }

    private static string[] ReadCrabPotStringArray(
        JsonElement source,
        string property)
    {
        return source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(property, out var values) &&
            values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? string.Empty)
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
    }

    private static EventCandidate BlockedMasterAnglerCrabPotCandidate(
        string reason,
        SmallModelActionParameter[] parameters)
    {
        return new EventCandidate
        {
            CandidateId = "master-angler:crab-pot:blocked",
            Kind = "collect_crab_pot",
            Available = false,
            ExpectedEffect =
                "master_angler_crab_pot_action_not_compiled;runtime_terminal_validation_required=true",
            AvailabilityClass = "master_angler_fail_closed",
            BlockReasons = new[]
            {
                string.IsNullOrWhiteSpace(reason)
                    ? "master_angler_intent_validation_failed"
                    : reason
            },
            Parameters = parameters
        };
    }
}
