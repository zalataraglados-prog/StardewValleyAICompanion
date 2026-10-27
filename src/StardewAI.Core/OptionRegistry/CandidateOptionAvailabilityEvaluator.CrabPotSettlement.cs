using System;
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
    private EventCandidate[] CrabPotSettlementCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) ||
            missing.Count == 0 ||
            !TryReadCompleteCrabPotNetwork(snapshot, out var networkRows) ||
            RemainingStageOneCrabPotCycles(snapshot) <= 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var producing = networkRows.Where(row =>
                ReadBool(row, "exact_base_crab_pot") == true &&
                ReadBool(row, "production_domain_complete") == true &&
                string.Equals(
                    ReadString(row, "service_status"),
                    "producing_or_waiting",
                    StringComparison.Ordinal) &&
                ReadCrabPotStringArray(row, "possible_qualified_item_ids")
                    .Any(missing.Contains))
            .ToArray();
        if (producing.Length == 0)
            return Array.Empty<EventCandidate>();

        var targetSpecies = producing
            .SelectMany(row => ReadCrabPotStringArray(
                row,
                "possible_qualified_item_ids"))
            .Where(missing.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var continuation = new[]
        {
            Parameter("continuation.option_id", "fishing.collect_crab_pots"),
            Parameter("continuation.crab_pot_stage", "await_native_crab_pot_day_update"),
            Parameter("master_angler_target_qualified_item_ids_json", JsonSerializer.Serialize(targetSpecies)),
            Parameter("crab_pot_producing_network_count", producing.Length.ToString()),
            Parameter("crab_pot_remaining_serviced_cycles", RemainingStageOneCrabPotCycles(snapshot).ToString())
        };
        var recovery = RecoveryCandidates(
                snapshot,
                Array.Empty<SmallModelActionParameter>())
            .Where(candidate => candidate.Available &&
                candidate.Kind is
                    "recovery_close_menu" or
                    "recovery_resume_sleep_prompt" or
                    "recovery_sleep_immediately" or
                    "recovery_return_home" or
                    "recovery_sleep_before_collapse")
            .ToArray();
        var selected = new[]
            {
                "recovery_resume_sleep_prompt",
                "recovery_sleep_immediately",
                "recovery_return_home",
                "recovery_close_menu",
                "recovery_sleep_before_collapse"
            }
            .Select(kind => recovery.FirstOrDefault(candidate =>
                string.Equals(candidate.Kind, kind, StringComparison.Ordinal)))
            .FirstOrDefault(candidate => candidate is not null);
        return selected is null
            ? Array.Empty<EventCandidate>()
            : new[]
            {
                CloneCandidate(
                    selected,
                    candidateId: "crab-pot-settlement:" +
                        selected.CandidateId,
                    expectedEffect: selected.ExpectedEffect +
                        ";crab_pot_native_day_update_pending=true;" +
                        "crab_pot_lifecycle_replan_after_settlement=true",
                    parameters: selected.Parameters
                        .Concat(continuation)
                        .ToArray(),
                    availabilityClass:
                        "transparent_crab_pot_native_overnight_settlement")
            };
    }
}
