using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// A single original terminal hop/deposit witness, never a sale or whole-lifecycle certificate.
public sealed partial class CropTeacherTerminalShipping : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherTerminalShipping(CropTeacherSupportingSource source, string stepId, CropTeacherTerminalShippingTarget target,
        string kind, IEnumerable<string> preparationPaths, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        Target = target; Kind = kind;
        var paths = preparationPaths.Concat(new[] { transition.QueuePath, transition.BeforeSnapshotPath,
                transition.ExecutionReceiptPath, transition.AfterSnapshotPath }).Select(Path.GetFullPath)
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var before = source.Snapshot;
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(transition.AfterSnapshotPath, "terminal native after");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(transition.QueuePath, "terminal native queue");
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath, "terminal native receipt");
        var selected = target.NativeVariants is null ? new CropTeacherTerminalVariant(target.SlotIndex, target.Quality, target.UnitPrice, target.InitialQuantity) :
            target.VariantAt(target.AcceptedDepositCount);
        Require(AtStand(after, target), "Terminal native after did not reach/remain on the exact Farm stand.");
        if (kind == "shipping_approach")
        {
            var proof = new MoneyRevenueTerminalQueueTransitionProof
            {
                TransitionId = stepId, QueuePath = transition.QueuePath, BeforeSnapshotPath = transition.BeforeSnapshotPath,
                ExecutionReceiptPath = transition.ExecutionReceiptPath, AfterSnapshotPath = transition.AfterSnapshotPath,
                RunId = transition.RunId, ExecutorVersion = transition.ExecutorVersion, SelectedCandidateId = transition.SelectedCandidateId
            };
            MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(before, new[] { proof },
                new("executor.ship_inventory_item_to_bin", target.QualifiedItemId, selected.SlotIndex, 1, selected.UnitPrice, string.Empty));
            CropTeacherNativeStepBoundary.Verify(before, after, before, receipt.StepResults.Single(), target.TotalDay, "Terminal approach");
            AcceptedDepositCountAfter = target.AcceptedDepositCount;
            VerifySource(after, target, AcceptedDepositCountAfter);
            PendingValueAfter = target.PendingValueAt(AcceptedDepositCountAfter);
        }
        else
        {
            Require(kind == "shipping_deposit" && AtStand(before, target), "Terminal deposit did not start at its exact stand.");
            var deposit = CropTeacherShippingDepositResources.Verify(before, after, queue, receipt, transition, target.QualifiedItemId);
            Require(deposit.SlotIndex == selected.SlotIndex && deposit.Quality == selected.Quality && deposit.UnitPrice == selected.UnitPrice &&
                    deposit.BinTileX == target.BinTileX && deposit.BinTileY == target.BinTileY &&
                    deposit.PendingValueBefore == target.PendingValueAt(target.AcceptedDepositCount),
                "Terminal native deposit differs from its selected variant/bin/accepted cursor.");
            AcceptedDepositCountAfter = checked(target.AcceptedDepositCount + 1);
            PendingValueAfter = deposit.PendingValueAfter;
            Require(PendingValueAfter == target.PendingValueAt(AcceptedDepositCountAfter), "Terminal actual pending value differs from its verified deposit cursor.");
            if (target.NativeVariants is not null) target.VerifyNativeVariantSource(after, AcceptedDepositCountAfter);
        }
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        rebind = new(after, source.Ledger, stepId, inputs.Validate, "Terminal shipping");
        inputs.Validate();
    }

    public CropTeacherTerminalShippingTarget Target { get; }
    public string Kind { get; }
    public int AcceptedDepositCountAfter { get; }
    public long PendingValueAfter { get; }
    public bool TerminalSettlementVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public string AfterSnapshotPath { get; }
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();

    private static void VerifySource(SnapshotEnvelope snapshot, CropTeacherTerminalShippingTarget target, int count)
    {
        if (target.NativeVariants is not null) { target.VerifyNativeVariantSource(snapshot, count); return; }
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(snapshot, "terminal source");
        Require(snapshot.SaveId.Value == target.SaveId && snapshot.PlayerId.Value == target.PlayerId && snapshot.GameVersion == target.GameVersion &&
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(snapshot, "time", "total_days") == target.TotalDay &&
                Field(snapshot, "player", "location_id").GetString() == "Farm", "Terminal source changed actor/game/day/location.");
        var slots = Field(snapshot, "player", "inventory").EnumerateArray().Where(row => Integer(row, "slot_index") == target.SlotIndex).ToArray();
        Require(slots is [var slot] && Text(slot, "qualified_item_id") == target.QualifiedItemId &&
                Integer(slot, "stack") == target.InitialQuantity - count && Integer(slot, "quality") == target.Quality &&
                Integer(slot, "sell_to_store_price") == target.UnitPrice && Boolean(slot, "can_be_shipped"),
            "Terminal source lost the exact retained stack/variant/shippability/price/cursor.");
        var bin = ReadBin(snapshot, target);
        Require(bin.GetProperty("pending_settlement_value").GetInt64() == checked((long)count * target.UnitPrice),
            "Terminal source pending value differs from its accepted deposit cursor.");
    }

    private static JsonElement ReadBin(SnapshotEnvelope snapshot, CropTeacherTerminalShippingTarget target)
    {
        var bins = Field(snapshot, "farm", "shipping_bins").EnumerateArray()
            .Where(row => Integer(row, "tile_x") == target.BinTileX && Integer(row, "tile_y") == target.BinTileY).ToArray();
        Require(bins is [var bin] && Boolean(bin, "completed") && Boolean(bin, "pending_settlement_value_complete") &&
                !string.IsNullOrWhiteSpace(Text(bin, "bin_scope")) && bin.GetProperty("player_id").ToString() == target.PlayerId &&
                Integer(bin, "interaction_stand_tile_x") == target.StandTileX && Integer(bin, "interaction_stand_tile_y") == target.StandTileY &&
                string.IsNullOrWhiteSpace(Text(bin, "interaction_stand_tile_blocked_reason")), "Terminal selected bin/stand/custody is unavailable or changed.");
        return bins.Single();
    }

    private static bool AtStand(SnapshotEnvelope snapshot, CropTeacherTerminalShippingTarget target) =>
        Field(snapshot, "player", "location_id").GetString() == "Farm" &&
        Field(snapshot, "player", "tile_x").GetInt32() == target.StandTileX && Field(snapshot, "player", "tile_y").GetInt32() == target.StandTileY;
    private static JsonElement Field(SnapshotEnvelope snapshot, string domain, string name)
    {
        Require(snapshot.State.TryGetValue(domain, out var fields) && fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("status", out var status) && FieldEnvelopeValidator.IsReadableStatus(status.GetString() ?? "") &&
                field.TryGetProperty("value", out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined,
            "Terminal source field unavailable: " + domain + "." + name);
        return snapshot.State[domain].GetProperty(name).GetProperty("value");
    }
    private static int Integer(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : -1;
    private static string Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static bool Boolean(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static bool SamePath(string first, string second) => CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
