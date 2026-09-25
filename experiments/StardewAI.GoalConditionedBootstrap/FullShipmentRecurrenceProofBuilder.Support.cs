using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private static bool EquivalentProgress(
        FullShipmentProgressCheckpoint left,
        FullShipmentProgressCheckpoint right) =>
        left.ShippedItemCount == right.ShippedItemCount &&
        left.Complete == right.Complete &&
        left.Achievement34 == right.Achievement34 &&
        left.MissingQualifiedItemIds.SequenceEqual(
            right.MissingQualifiedItemIds,
            StringComparer.Ordinal) &&
        left.Items.Count == right.Items.Count &&
        left.Items.All(pair =>
            right.Items.TryGetValue(pair.Key, out var value) &&
            value == pair.Value) &&
        left.ShippingCollection.Count == right.ShippingCollection.Count &&
        left.ShippingCollection.All(pair =>
            right.ShippingCollection.TryGetValue(pair.Key, out var value) &&
            value == pair.Value);

    private static void RequireSameActor(
        SnapshotEnvelope left,
        SnapshotEnvelope right)
    {
        Require(
            !string.IsNullOrWhiteSpace(left.SaveId.Value) &&
            left.SaveId.Value == right.SaveId.Value &&
            !string.IsNullOrWhiteSpace(left.PlayerId.Value) &&
            left.PlayerId.Value == right.PlayerId.Value &&
            left.GameVersion == right.GameVersion,
            "Full Shipment recurrence snapshot actor or game identity drifted.");
    }

    private static SnapshotEnvelope ReadSnapshot(string path, string label) =>
        CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, label);

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record VerifiedAcquisition(
        string AfterSnapshotPath,
        SnapshotEnvelope AfterSnapshot,
        string ProofReceiptSha256);

    private sealed record VerifiedDeposit(
        string AfterSnapshotPath,
        SnapshotEnvelope AfterSnapshot,
        string TeacherReceiptSha256);

    private sealed record VerifiedSettlement(
        string AfterSnapshotPath,
        SnapshotEnvelope AfterSnapshot,
        string SettlementReceiptSha256,
        int BeforeShippedItemCount,
        int AfterShippedItemCount,
        int StartTotalDay,
        int EndTotalDay,
        bool TerminalTransition);
}
