using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private sealed record AcquisitionMachineCapacityRequest(
        string GoalId,
        string RouteOccurrenceId,
        string RouteKind,
        string SourceId,
        string OutputQualifiedItemId,
        string MachineQualifiedItemId,
        string IntentId,
        string RequestedCandidateKind)
    {
        public MachineAcquisitionRouteSupportSource SupportSource => new(
            GoalId,
            RouteOccurrenceId,
            RouteKind,
            SourceId,
            OutputQualifiedItemId,
            MachineQualifiedItemId);

        public string SupportSourcesJson =>
            JsonSerializer.Serialize(SupportSource);
    }

    private EventCandidate[] AcquisitionMachineCapacityCandidates(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger,
        AcquisitionMachineCapacityRequest request)
    {
        var active = commitmentLedger?.MachineSupportIntents
            .Where(intent => string.Equals(
                intent.Status,
                StrategyCommitmentStatuses.Active,
                StringComparison.Ordinal))
            .OrderBy(intent => intent.IntentId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<MachineSupportIntent>();
        if (active.Length > 0)
        {
            if (active.Length != 1 ||
                !string.Equals(
                    active[0].IntentId,
                    request.IntentId,
                    StringComparison.Ordinal) ||
                !MachineSupportIntentProjection.AcquisitionRouteIntentIsValid(
                    active[0]) ||
                !string.Equals(
                    active[0].SupportSourcesJson,
                    request.SupportSourcesJson,
                    StringComparison.Ordinal))
            {
                return Array.Empty<EventCandidate>();
            }

            if (string.Equals(
                    request.RequestedCandidateKind,
                    "craft_machine_item",
                    StringComparison.Ordinal))
            {
                return AcquisitionCraftCandidates(
                    snapshot,
                    commitmentLedger,
                    request,
                    active[0]);
            }

            return ContinueAcquisitionMachineCapacity(
                snapshot,
                commitmentLedger,
                active[0]);
        }

        var placements = MachinePlacementCandidates(snapshot, commitmentLedger)
            .Where(candidate => string.Equals(
                candidate.QualifiedItemId,
                request.MachineQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        if (placements.Length > 0)
        {
            return new[]
            {
                AttachInitialAcquisitionPlacement(
                    placements[0],
                    snapshot,
                    request)
            };
        }

        return AcquisitionCraftCandidates(
            snapshot,
            commitmentLedger,
            request,
            null);
    }

    private EventCandidate[] ContinueAcquisitionMachineCapacity(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger,
        MachineSupportIntent intent)
    {
        if (string.Equals(
                intent.Stage,
                MachineSupportIntentStages.PlacementBound,
                StringComparison.Ordinal))
        {
            var loads = SupportedMachineInputCandidates(
                    snapshot,
                    commitmentLedger)
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_support_intent_id"),
                    intent.IntentId,
                    StringComparison.Ordinal))
                .ToArray();
            if (loads.Length > 0)
            {
                return loads;
            }
        }

        return MachinePlacementCandidates(snapshot, commitmentLedger)
            .Where(candidate => string.Equals(
                ReadParameter(
                    candidate.Parameters,
                    "machine_support_intent_id"),
                intent.IntentId,
                StringComparison.Ordinal))
            .ToArray();
    }

    private static EventCandidate[] AcquisitionCraftCandidates(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger,
        AcquisitionMachineCapacityRequest request,
        MachineSupportIntent? activeIntent)
    {
        return MachineCraftingCandidates(snapshot, commitmentLedger)
            .Where(candidate => string.Equals(
                candidate.QualifiedItemId,
                request.MachineQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
            .Select(candidate => AttachAcquisitionCraft(
                candidate,
                request,
                activeIntent))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Take(1)
            .ToArray();
    }

    private static EventCandidate AttachAcquisitionCraft(
        EventCandidate candidate,
        AcquisitionMachineCapacityRequest request,
        MachineSupportIntent? activeIntent)
    {
        const string noDemand =
            "machine_recipe_has_no_proven_task_production_or_collection_requirement";
        candidate.BlockReasons = candidate.BlockReasons
            .Where(reason => !string.Equals(
                reason,
                noDemand,
                StringComparison.Ordinal))
            .ToArray();
        candidate.Available = candidate.BlockReasons.Length == 0;
        candidate.ExpectedEffect +=
            ";machine_acquisition_route_support_json=" +
            request.SupportSourcesJson +
            ";machine_capacity_support_intent_id=" +
            request.IntentId;
        candidate.Parameters = candidate.Parameters
            .Concat(new[]
            {
                Parameter(
                    "machine_acquisition_route_support_json",
                    request.SupportSourcesJson),
                Parameter(
                    "machine_capacity_support_intent_id",
                    request.IntentId)
            })
            .Concat(activeIntent is null
                ? Array.Empty<SmallModelActionParameter>()
                : new[]
                {
                    Parameter(
                        "machine_support_intent_revision",
                        activeIntent.Revision.ToString()),
                    Parameter(
                        "machine_support_intent_stage",
                        activeIntent.Stage),
                    Parameter(
                        "machine_support_intent_source_state_hash",
                        activeIntent.SourceStateHash),
                    Parameter(
                        "machine_support_sources_json",
                        activeIntent.SupportSourcesJson)
                })
            .ToArray();
        return candidate;
    }

    private static EventCandidate AttachInitialAcquisitionPlacement(
        EventCandidate candidate,
        SnapshotEnvelope snapshot,
        AcquisitionMachineCapacityRequest request)
    {
        var continuation = new MachineSupportContinuation(
            "active",
            "place_acquisition_route_supported_machine",
            request.IntentId,
            1,
            MachineSupportIntentStages.PlacementBound,
            snapshot.StateHash,
            request.GoalId,
            "acquisition_route_requirement",
            request.SupportSourcesJson,
            0,
            0,
            0.12,
            "exact_acquisition_route_requires_inventory_machine_placement");
        candidate.ExpectedEffect +=
            ";machine_acquisition_route_support_json=" +
            request.SupportSourcesJson +
            MachineSupportIntentProjection.ExpectedEffectSuffix(continuation);
        candidate.Parameters = candidate.Parameters
            .Concat(MachineSupportIntentProjection.Parameters(continuation))
            .ToArray();
        return candidate;
    }

    private static bool TryReadAcquisitionMachineCapacityRequest(
        SmallModelActionParameter[]? parameters,
        out AcquisitionMachineCapacityRequest request)
    {
        request = new(string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty);
        var values = parameters ?? Array.Empty<SmallModelActionParameter>();
        string Read(string name)
        {
            var matches = values.Where(parameter => string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal))
                .Select(parameter => parameter.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return matches.Length == 1 ? matches[0] : string.Empty;
        }

        if (!string.Equals(
                Read("machine_capacity_support_kind"),
                "acquisition_route",
                StringComparison.Ordinal))
        {
            return false;
        }

        var parsed = new AcquisitionMachineCapacityRequest(
            Read("machine_capacity_goal_id"),
            Read("machine_capacity_route_occurrence_id"),
            Read("machine_capacity_route_kind"),
            Read("machine_capacity_source_id"),
            Read("machine_capacity_output_qualified_item_id"),
            Read("machine_capacity_machine_qualified_item_id"),
            Read("machine_capacity_intent_id"),
            Read("machine_capacity_requested_candidate_kind"));
        if (string.IsNullOrWhiteSpace(parsed.GoalId) ||
            string.IsNullOrWhiteSpace(parsed.RouteOccurrenceId) ||
            string.IsNullOrWhiteSpace(parsed.RouteKind) ||
            string.IsNullOrWhiteSpace(parsed.SourceId) ||
            string.IsNullOrWhiteSpace(parsed.OutputQualifiedItemId) ||
            string.IsNullOrWhiteSpace(parsed.MachineQualifiedItemId) ||
            string.IsNullOrWhiteSpace(parsed.IntentId) ||
            parsed.RequestedCandidateKind is not (
                "" or "craft_machine_item" or "place_machine_item"))
        {
            return false;
        }

        request = parsed;
        return true;
    }
}
