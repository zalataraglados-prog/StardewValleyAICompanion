using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    private static readonly HashSet<string> ResourceNeutralRouteOptions = new(
        new[]
        {
            "executor.move_to_tile",
            "executor.traverse_connector",
            "executor.interact",
            "executor.choose_dialogue_response",
            "executor.close_menu",
            "executor.wait_ticks"
        },
        StringComparer.Ordinal);

    private static TerminalRouteVerification VerifyRouteTransitions(
        MoneyRevenueCounterfactualProofRequest request,
        SnapshotEnvelope terminalStart,
        MoneyRevenueTerminalQueueTransitionProof[] transitions)
    {
        transitions ??= Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();
        Require(transitions.All(row => row is not null &&
                    !string.IsNullOrWhiteSpace(row.TransitionId)) &&
                transitions.Select(row => row.TransitionId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    transitions.Length,
            "Money Revenue terminal route transition identity is invalid.");
        var expected = terminalStart;
        var expectedPath = request.TerminalSnapshotPath;
        var evidence = new List<MoneyRevenueTerminalQueueTransitionEvidence>();
        for (var index = 0; index < transitions.Length; index++)
        {
            var proof = transitions[index];
            var queuePath = RequiredFile(
                proof.QueuePath,
                "Money Revenue terminal route queue");
            var beforePath = RequiredFile(
                proof.BeforeSnapshotPath,
                "Money Revenue terminal route before snapshot");
            var receiptPath = RequiredFile(
                proof.ExecutionReceiptPath,
                "Money Revenue terminal route execution receipt");
            var afterPath = RequiredFile(
                proof.AfterSnapshotPath,
                "Money Revenue terminal route after snapshot");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                queuePath,
                "Money Revenue terminal route queue");
            var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                beforePath,
                "Money Revenue terminal route before snapshot");
            var receipt = CurrentTeacherFrontierSupport.Read<
                QueueExecutionReceiptEnvelope>(
                receiptPath,
                "Money Revenue terminal route execution receipt");
            var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                afterPath,
                "Money Revenue terminal route after snapshot");
            Require(EqualJson(before, expected) &&
                    before.StateHash == expected.StateHash,
                "Money Revenue terminal route is not checkpoint-contiguous at " +
                proof.TransitionId + ".");
            var reasons = QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                receipt,
                after,
                proof.RunId,
                proof.ExecutorVersion,
                proof.SelectedCandidateId,
                before.StateHash,
                requireTeacherPreferenceStateRebound: false);
            Require(reasons.Length == 0,
                "Money Revenue terminal route receipt was rejected: " +
                string.Join(",", reasons));
            var optionIds = (queue.Items ?? Array.Empty<ActionQueueItem>())
                .Select(row => row.OptionId)
                .ToArray();
            Require(optionIds.Length > 0 &&
                    optionIds.All(ResourceNeutralRouteOptions.Contains) &&
                    QueueBindsTerminalIdentity(queue, request),
                "Money Revenue terminal route is not a resource-neutral continuation of its request.");
            Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(
                        before,
                        out var beforeBoundary) &&
                    FullShipmentRecoverySettlementBoundaryProjector.TryProject(
                        after,
                        out var afterBoundary) &&
                    beforeBoundary == afterBoundary,
                "Money Revenue terminal route changed persistent resources.");
            evidence.Add(new MoneyRevenueTerminalQueueTransitionEvidence(
                index,
                proof.TransitionId,
                Hash(queuePath),
                Hash(beforePath),
                Hash(receiptPath),
                Hash(afterPath),
                before.StateHash,
                after.StateHash,
                before.GameTick,
                after.GameTick,
                optionIds));
            expected = after;
            expectedPath = afterPath;
        }
        return new TerminalRouteVerification(
            expected,
            expectedPath,
            evidence.ToArray());
    }

    private static bool QueueBindsTerminalIdentity(
        ActionQueueEnvelope queue,
        MoneyRevenueCounterfactualProofRequest request) =>
        (queue.Items ?? Array.Empty<ActionQueueItem>()).Any(item =>
            ParametersBindTerminalIdentity(
                item.NormalizedCommand?.Parameters ??
                    Array.Empty<SmallModelActionParameter>(),
                request));

    private static bool ParametersBindTerminalIdentity(
        SmallModelActionParameter[] parameters,
        MoneyRevenueCounterfactualProofRequest request)
    {
        var candidate = request.TerminalCandidate;
        return SingleParameter(parameters, "continuation.option_id") ==
                    request.TerminalOptionId &&
            SingleParameter(parameters, "continuation.qualified_item_id") ==
                    candidate.QualifiedItemId &&
            SingleIntParameter(parameters, "continuation.slot_index") ==
                    candidate.SlotIndex &&
            SingleIntParameter(parameters, "continuation.quantity") ==
                    candidate.Quantity &&
            SingleIntParameter(
                parameters,
                "continuation.expected_unit_price") ==
                    candidate.UnitPrice &&
            (request.TerminalOptionId != "economy.sell_items" ||
             SingleParameter(parameters, "continuation.shop_id") ==
                candidate.ShopId);
    }

    private static string SingleParameter(
        SmallModelActionParameter[] parameters,
        string name)
    {
        var values = parameters.Where(row => row.Name == name)
            .Select(row => row.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static int? SingleIntParameter(
        SmallModelActionParameter[] parameters,
        string name) => int.TryParse(
            SingleParameter(parameters, name),
            out var value)
                ? value
                : null;

    private sealed record TerminalRouteVerification(
        SnapshotEnvelope TerminalActionBefore,
        string TerminalActionBeforePath,
        MoneyRevenueTerminalQueueTransitionEvidence[] Evidence);
}
