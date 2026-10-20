using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyRevenueResourceNeutralTerminalRouteVerifier
{
    internal static readonly HashSet<string> ResourceNeutralOptions = new(
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

    private static readonly HashSet<string> HomeIngressOptions = new(
        new[]
        {
            "executor.move_to_tile",
            "executor.traverse_connector",
            "executor.wait_ticks"
        },
        StringComparer.Ordinal);

    internal static MoneyRevenueResourceNeutralTerminalRouteVerification Verify(
        SnapshotEnvelope start,
        MoneyRevenueTerminalQueueTransitionProof[] transitions,
        MoneyRevenueTerminalContinuationIdentity identity) => VerifyCore(
            start,
            transitions,
            identity,
            ResourceNeutralOptions,
            targetLocation: string.Empty,
            requireMovement: false);

    internal static MoneyRevenueResourceNeutralTerminalRouteVerification
        VerifyHomeIngress(
            SnapshotEnvelope start,
            MoneyRevenueTerminalQueueTransitionProof[] transitions)
    {
        var homeLocation = ReadHomeLocation(start);
        return VerifyCore(
            start,
            transitions,
            identity: null,
            HomeIngressOptions,
            homeLocation,
            requireMovement: true);
    }

    private static MoneyRevenueResourceNeutralTerminalRouteVerification
        VerifyCore(
            SnapshotEnvelope start,
            MoneyRevenueTerminalQueueTransitionProof[] transitions,
            MoneyRevenueTerminalContinuationIdentity? identity,
            HashSet<string> allowedOptions,
            string targetLocation,
            bool requireMovement)
    {
        transitions ??= Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();
        Require(transitions.All(row => row is not null &&
                    !string.IsNullOrWhiteSpace(row.TransitionId)) &&
                transitions.Select(row => row.TransitionId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    transitions.Length,
            "Money Revenue terminal route transition identity is invalid.");
        var expected = start;
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
            Require(BootstrapArtifactGuard.EqualJson(before, expected) &&
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
                    optionIds.All(allowedOptions.Contains) &&
                    (!requireMovement || optionIds.Any(row => row is
                        "executor.move_to_tile" or
                        "executor.traverse_connector")) &&
                    (identity is null ||
                        QueueBindsTerminalIdentity(queue, identity)),
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
        }
        Require(string.IsNullOrWhiteSpace(targetLocation) ||
                ReadPlayerLocation(expected) == targetLocation,
            "Money Revenue terminal route did not reach the required location.");
        return new MoneyRevenueResourceNeutralTerminalRouteVerification(
            expected,
            evidence.ToArray());
    }

    private static string ReadPlayerLocation(SnapshotEnvelope snapshot)
    {
        Require(TryStateFieldValue(
                    snapshot,
                    "player",
                    "location_id",
                    out var location) &&
                location.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(location.GetString()),
            "Money Revenue terminal route player location is unavailable.");
        return location.GetString() ?? string.Empty;
    }

    private static string ReadHomeLocation(SnapshotEnvelope snapshot)
    {
        var location = default(JsonElement);
        Require(TryStateFieldValue(
                    snapshot,
                    "current_location",
                    "home_context",
                    out var context) &&
                context.ValueKind == JsonValueKind.Object &&
                context.TryGetProperty("home_location_id", out location) &&
                location.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(location.GetString()),
            "Money Revenue terminal route home location is unavailable.");
        return location.GetString() ?? string.Empty;
    }

    private static bool TryStateFieldValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var parent) &&
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool QueueBindsTerminalIdentity(
        ActionQueueEnvelope queue,
        MoneyRevenueTerminalContinuationIdentity identity) =>
        (queue.Items ?? Array.Empty<ActionQueueItem>()).Any(item =>
            ParametersBindTerminalIdentity(
                item.NormalizedCommand?.Parameters ??
                    Array.Empty<SmallModelActionParameter>(),
                identity));

    private static bool ParametersBindTerminalIdentity(
        SmallModelActionParameter[] parameters,
        MoneyRevenueTerminalContinuationIdentity identity) =>
        SingleParameter(parameters, "continuation.option_id") ==
            identity.OptionId &&
        SingleParameter(parameters, "continuation.qualified_item_id") ==
            identity.QualifiedItemId &&
        SingleIntParameter(parameters, "continuation.slot_index") ==
            identity.SlotIndex &&
        SingleIntParameter(parameters, "continuation.quantity") ==
            identity.Quantity &&
        SingleIntParameter(parameters, "continuation.expected_unit_price") ==
            identity.UnitPrice &&
        (identity.OptionId != "economy.sell_items" ||
         SingleParameter(parameters, "continuation.shop_id") ==
            identity.ShopId);

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

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}

internal sealed record MoneyRevenueTerminalContinuationIdentity(
    string OptionId,
    string QualifiedItemId,
    int? SlotIndex,
    int Quantity,
    int UnitPrice,
    string ShopId);

internal sealed record MoneyRevenueResourceNeutralTerminalRouteVerification(
    SnapshotEnvelope TerminalActionBefore,
    MoneyRevenueTerminalQueueTransitionEvidence[] Evidence);
