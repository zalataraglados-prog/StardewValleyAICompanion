using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private const int ItemPlacedInMachineTrigger = 1;
    private const string ExactContextTags =
        "exact_item_get_context_tags";
    private const string ExactObjectEdibility = "exact_object_edibility";

    internal static AcquisitionRouteTargetDateResource EvaluateMachine(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionResourceInputSnapshotState state)
    {
        var source = staticRoute.MachineSource;
        if (source is null)
            return Blocked(route, MachineInput,
                "authoritative_machine_source_missing");
        if (source.Triggers.Length == 0)
            return Blocked(route, MachineInput,
                "authoritative_machine_trigger_missing");

        var inputTriggers = source.Triggers.Where(trigger =>
                (trigger.Trigger & ItemPlacedInMachineTrigger) != 0)
            .ToArray();
        if (inputTriggers.Length == 0)
            return NotRequired(route, MachineInput);
        if (inputTriggers.Length != source.Triggers.Length)
        {
            return Blocked(route, MachineInput,
                "machine_mixed_input_and_automatic_trigger_requires_route_expansion");
        }
        if (!state.MaterialEvidenceAvailable)
            return Blocked(route, MachineInput,
                state.MaterialBlockingReasons);

        var guaranteedOutputPerAttempt = Math.Max(1, source.MinimumStack);
        var attemptCount = AcquisitionQuantityMath.DivideRoundUp(
            RequiredAmount(route),
            guaranteedOutputPerAttempt);
        var blockedReasons = new List<string>();
        MachineInputAttempt? firstMiss = null;
        foreach (var trigger in inputTriggers)
        {
            var parsed = ParseMachineInputCondition(trigger.Condition);
            if (!parsed.Supported)
            {
                blockedReasons.Add(
                    "machine_input_trigger_condition_unsupported:" +
                    StableToken(trigger.Id));
                continue;
            }
            if (trigger.RequiredCount <= 0)
            {
                blockedReasons.Add(
                    "machine_input_trigger_required_count_invalid:" +
                    StableToken(trigger.Id));
                continue;
            }

            var candidates = MachinePrimaryCandidates(
                trigger,
                parsed,
                state.MaterialSlots);
            foreach (var candidate in candidates.Candidates)
            {
                var attempt = EvaluateMachineInputAttempt(
                    source,
                    trigger,
                    parsed,
                    candidate,
                    attemptCount,
                    state.MaterialSlots);
                firstMiss ??= attempt;
                if (attempt.Matches)
                {
                    return Result(
                        route,
                        "resolved_resource_inputs_match",
                        true,
                        true,
                        MachineInput,
                        attempt.Evaluations,
                        Array.Empty<string>(),
                        Array.Empty<string>());
                }
            }
            if (candidates.MetadataCouldChangeResult)
            {
                blockedReasons.Add(
                    "machine_input_item_metadata_incomplete:" +
                    StableToken(trigger.Id));
            }
            if (candidates.Candidates.Length == 0 &&
                !candidates.MetadataCouldChangeResult)
            {
                firstMiss ??= MissingMachineInputAttempt(
                    trigger,
                    parsed,
                    attemptCount,
                    state.MaterialSlots);
            }
        }

        if (blockedReasons.Count > 0)
            return Blocked(route, MachineInput, blockedReasons.ToArray());
        if (firstMiss is null)
            return Blocked(route, MachineInput,
                "machine_input_candidate_domain_unresolved");
        return Result(
            route,
            "resolved_resource_inputs_miss",
            true,
            false,
            MachineInput,
            firstMiss.Evaluations,
            firstMiss.NonMatchingReasons,
            Array.Empty<string>());
    }

    private static MachineInputAttempt EvaluateMachineInputAttempt(
        AcquisitionMachineSourceEvidence source,
        AcquisitionMachineTriggerEvidence trigger,
        MachineInputCondition condition,
        MachinePrimaryCandidate candidate,
        int attemptCount,
        AcquisitionResourceMaterialSlot[] slots)
    {
        var remainingBySlot = slots.ToDictionary(
            slot => SlotKey(slot.NodeId, slot.SlotIndex),
            slot => slot.AvailableQuantity,
            StringComparer.Ordinal);
        var evaluations = new List<AcquisitionResourceInputEvaluation>();
        var misses = new List<string>();
        var primaryRequired = AcquisitionQuantityMath.Multiply(
            trigger.RequiredCount,
            attemptCount);
        evaluations.Add(AllocateMachineInput(
            "machine_primary_input",
            candidate.QualifiedItemId,
            primaryRequired,
            trigger.RequiredCount,
            attemptCount,
            trigger,
            condition,
            candidate.EligibleSlots,
            remainingBySlot,
            misses));

        foreach (var additional in source.AdditionalConsumedItems
                     .GroupBy(item => QualifyObjectId(item.ItemId),
                         StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var perAttempt = additional.Sum(item => item.RequiredCount);
            if (perAttempt <= 0)
            {
                misses.Add(
                    "machine_additional_input_required_count_invalid:" +
                    additional.Key);
                continue;
            }
            var eligible = slots.Where(slot =>
                    slot.QualifiedItemId == additional.Key)
                .ToArray();
            evaluations.Add(AllocateMachineInput(
                "machine_additional_input",
                additional.Key,
                AcquisitionQuantityMath.Multiply(perAttempt, attemptCount),
                perAttempt,
                attemptCount,
                trigger,
                MachineInputCondition.Empty,
                eligible,
                remainingBySlot,
                misses));
        }
        return new MachineInputAttempt(
            misses.Count == 0,
            evaluations.ToArray(),
            misses.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static AcquisitionResourceInputEvaluation AllocateMachineInput(
        string inputKind,
        string qualifiedItemId,
        int requiredQuantity,
        int perAttemptQuantity,
        int attemptCount,
        AcquisitionMachineTriggerEvidence trigger,
        MachineInputCondition condition,
        AcquisitionResourceMaterialSlot[] eligibleSlots,
        IDictionary<string, int> remainingBySlot,
        ICollection<string> misses)
    {
        var available = eligibleSlots.Sum(slot =>
        {
            var key = SlotKey(slot.NodeId, slot.SlotIndex);
            return remainingBySlot.TryGetValue(key, out var quantity)
                ? quantity
                : 0;
        });
        var remaining = requiredQuantity;
        foreach (var slot in eligibleSlots
                     .OrderBy(slot => slot.NodeId, StringComparer.Ordinal)
                     .ThenBy(slot => slot.SlotIndex))
        {
            var key = SlotKey(slot.NodeId, slot.SlotIndex);
            var quantity = Math.Min(remaining, remainingBySlot[key]);
            remainingBySlot[key] -= quantity;
            remaining -= quantity;
            if (remaining == 0)
                break;
        }
        var matches = remaining == 0;
        if (!matches)
        {
            misses.Add(
                "required_machine_resource_quantity_unavailable:" +
                qualifiedItemId);
        }
        return new AcquisitionResourceInputEvaluation(
            inputKind,
            qualifiedItemId,
            requiredQuantity,
            available,
            matches
                ? "resolved_resource_input_match"
                : "resolved_resource_input_miss",
            new[]
            {
                "state.farm.material_inventory_graph.value." +
                "inventory_nodes[].slots[]",
                "static_route.machine_source.triggers[]"
            },
            Array.Empty<string>(),
            new AcquisitionMachineResourceBinding(
                trigger.Id,
                trigger.Condition,
                trigger.RequiredTags.Concat(condition.RequiredTags)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                condition.MinimumEdibility,
                condition.MaximumEdibility,
                perAttemptQuantity,
                attemptCount,
                eligibleSlots.Select(slot =>
                        new AcquisitionMachineResourceSlot(
                            slot.NodeId,
                            slot.SlotIndex,
                            slot.QualifiedItemId,
                            slot.AvailableQuantity))
                    .ToArray()));
    }

    private static MachinePrimaryCandidateSet MachinePrimaryCandidates(
        AcquisitionMachineTriggerEvidence trigger,
        MachineInputCondition condition,
        AcquisitionResourceMaterialSlot[] slots)
    {
        var requiredItemId = string.IsNullOrWhiteSpace(trigger.RequiredItemId)
            ? null
            : QualifyObjectId(trigger.RequiredItemId);
        var requiredTags = trigger.RequiredTags
            .Concat(condition.RequiredTags)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var relevant = slots.Where(slot => requiredItemId is null ||
                slot.QualifiedItemId == requiredItemId)
            .ToArray();
        var unknown = false;
        var matched = new List<AcquisitionResourceMaterialSlot>();
        foreach (var slot in relevant)
        {
            if (requiredTags.Length > 0 &&
                slot.ContextTagsProjectionStatus != ExactContextTags)
            {
                unknown = true;
                continue;
            }
            if ((condition.MinimumEdibility.HasValue ||
                 condition.MaximumEdibility.HasValue) &&
                slot.EdibilityProjectionStatus != ExactObjectEdibility)
            {
                if (slot.EdibilityProjectionStatus ==
                    "not_applicable_non_object_item")
                {
                    continue;
                }
                unknown = true;
                continue;
            }
            if (!requiredTags.All(tag => slot.ContextTags.Contains(
                    tag,
                    StringComparer.Ordinal)))
            {
                continue;
            }
            if (condition.ForbiddenAllTagSets.Any(tags => tags.All(tag =>
                    slot.ContextTags.Contains(tag, StringComparer.Ordinal))))
            {
                continue;
            }
            if (condition.MinimumEdibility.HasValue &&
                slot.Edibility < condition.MinimumEdibility.Value)
            {
                continue;
            }
            if (condition.MaximumEdibility.HasValue &&
                slot.Edibility > condition.MaximumEdibility.Value)
            {
                continue;
            }
            matched.Add(slot);
        }
        var candidates = matched
            .GroupBy(slot => slot.QualifiedItemId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MachinePrimaryCandidate(
                group.Key,
                group.ToArray()))
            .ToArray();
        return new MachinePrimaryCandidateSet(candidates, unknown);
    }

    private static MachineInputAttempt MissingMachineInputAttempt(
        AcquisitionMachineTriggerEvidence trigger,
        MachineInputCondition condition,
        int attemptCount,
        AcquisitionResourceMaterialSlot[] slots)
    {
        var qualifiedItemId = string.IsNullOrWhiteSpace(trigger.RequiredItemId)
            ? string.Empty
            : QualifyObjectId(trigger.RequiredItemId);
        var required = AcquisitionQuantityMath.Multiply(
            trigger.RequiredCount,
            attemptCount);
        var evaluation = new AcquisitionResourceInputEvaluation(
            "machine_primary_input",
            qualifiedItemId,
            required,
            0,
            "resolved_resource_input_miss",
            new[]
            {
                "state.farm.material_inventory_graph.value." +
                "inventory_nodes[].slots[]",
                "static_route.machine_source.triggers[]"
            },
            Array.Empty<string>(),
            new AcquisitionMachineResourceBinding(
                trigger.Id,
                trigger.Condition,
                trigger.RequiredTags.Concat(condition.RequiredTags)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                condition.MinimumEdibility,
                condition.MaximumEdibility,
                trigger.RequiredCount,
                attemptCount,
                Array.Empty<AcquisitionMachineResourceSlot>()));
        return new MachineInputAttempt(
            false,
            new[] { evaluation },
            new[] { "required_machine_input_candidate_unavailable" });
    }

    private static MachineInputCondition ParseMachineInputCondition(
        string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return MachineInputCondition.Empty;
        var requiredTags = new List<string>();
        var forbiddenTagSets = new List<string[]>();
        int? minimumEdibility = null;
        int? maximumEdibility = null;
        foreach (var clause in condition.Split(',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var tokens = clause.Split(' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
                return MachineInputCondition.Unsupported;
            var negated = tokens[0].StartsWith('!');
            var name = negated ? tokens[0][1..] : tokens[0];
            if (name is "RANDOM" or "SYNCED_RANDOM")
                continue;
            if (name == "ITEM_CONTEXT_TAG" && tokens.Length >= 3 &&
                tokens[1].Equals("Input", StringComparison.OrdinalIgnoreCase))
            {
                if (negated)
                    forbiddenTagSets.Add(tokens[2..]);
                else
                    requiredTags.AddRange(tokens[2..]);
                continue;
            }
            if (!negated && name == "ITEM_EDIBILITY" &&
                tokens.Length is >= 2 and <= 4 &&
                tokens[1].Equals("Input", StringComparison.OrdinalIgnoreCase) &&
                (tokens.Length < 3 || int.TryParse(tokens[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)) &&
                (tokens.Length < 4 || int.TryParse(tokens[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)))
            {
                minimumEdibility = tokens.Length >= 3
                    ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
                    : -299;
                maximumEdibility = tokens.Length >= 4
                    ? int.Parse(tokens[3], CultureInfo.InvariantCulture)
                    : int.MaxValue;
                continue;
            }
            return MachineInputCondition.Unsupported;
        }
        return new MachineInputCondition(
            true,
            requiredTags.Distinct(StringComparer.Ordinal).ToArray(),
            forbiddenTagSets.ToArray(),
            minimumEdibility,
            maximumEdibility);
    }

    private static string StableToken(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unnamed" : value;

    private static string SlotKey(string nodeId, int slotIndex) =>
        nodeId + "#" + slotIndex;

    private sealed record MachinePrimaryCandidate(
        string QualifiedItemId,
        AcquisitionResourceMaterialSlot[] EligibleSlots);

    private sealed record MachinePrimaryCandidateSet(
        MachinePrimaryCandidate[] Candidates,
        bool MetadataCouldChangeResult);

    private sealed record MachineInputAttempt(
        bool Matches,
        AcquisitionResourceInputEvaluation[] Evaluations,
        string[] NonMatchingReasons);

    private sealed record MachineInputCondition(
        bool Supported,
        string[] RequiredTags,
        string[][] ForbiddenAllTagSets,
        int? MinimumEdibility,
        int? MaximumEdibility)
    {
        public static MachineInputCondition Empty { get; } = new(
            true,
            Array.Empty<string>(),
            Array.Empty<string[]>(),
            null,
            null);

        public static MachineInputCondition Unsupported { get; } = new(
            false,
            Array.Empty<string>(),
            Array.Empty<string[]>(),
            null,
            null);
    }
}
