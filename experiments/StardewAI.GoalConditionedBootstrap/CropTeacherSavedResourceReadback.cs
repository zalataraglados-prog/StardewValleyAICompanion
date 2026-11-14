using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// Content comparison only. A controller must independently establish actual saved-file load provenance.
public sealed partial class CropTeacherSavedResourceReadback : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherSavedResourceReadback(string beforeSnapshotPath, string afterSnapshotPath, string ledgerPath, string stepId,
        bool nativeLoadInitialization = false)
    {
        var paths = new[] { beforeSnapshotPath, afterSnapshotPath, ledgerPath };
        Require(paths.All(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)),
            "requires three absolute input paths.");
        paths = paths.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == 3,
            "requires three distinct input paths.");
        using var reads = InvocationReadSession.Start(paths);
        inputs = new(paths);
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[0], "saved-resource before");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[1], "saved-resource after");
        VerifySnapshotContentCore(before, after, nativeLoadInitialization);

        var ledger = AcquisitionStrategyLedgerReader.Read(
            CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(paths[2], "saved-resource source ledger"), before).Ledger;
        Require(ledger.SourceStateHash == before.StateHash, "ledger source must equal the before snapshot.");
        Require(ledger.PreparedMaterialTransfer is null, "cannot cross an unresolved prepared material transfer.");
        Require(!string.IsNullOrWhiteSpace(stepId) &&
            !ledger.History.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId) &&
            !ledger.CropPlantingCommitments.Any(row => row.CommitmentId == stepId || row.SourceDecisionId == stepId) &&
            !ledger.MaterialReservations.Any(row => row.ReservationId == stepId || row.SourceDecisionId == stepId) &&
            !ledger.CurrencyReservations.Any(row => row.ReservationId == stepId || row.SourceDecisionId == stepId),
            "marker identity is not new/distinct.");
        AfterSnapshotPath = paths[1];
        rebind = new(after, ledger, stepId, inputs.Validate, "Crop saved-resource readback");
        ValidateInputsUnchanged();
    }

    public static CropTeacherSavedResourceReadback Open(string beforeSnapshotPath, string afterSnapshotPath,
        string ledgerPath, string stepId) => new(beforeSnapshotPath, afterSnapshotPath, ledgerPath, stepId);

    public static CropTeacherSavedResourceReadback OpenNativeLoad(string beforeSnapshotPath, string afterSnapshotPath,
        string ledgerPath, string stepId) => new(beforeSnapshotPath, afterSnapshotPath, ledgerPath, stepId, nativeLoadInitialization: true);

    public string AfterSnapshotPath { get; }
    public bool SnapshotResourceContentMatches => true;
    public bool NativeReloadVerified => false;
    public bool SemanticSavedContentVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes();
    public void ValidateInputsUnchanged() => inputs.Validate();

    public static void VerifySnapshotContent(SnapshotEnvelope before, SnapshotEnvelope after)
        => VerifySnapshotContentCore(before, after, nativeLoadInitialization: false);

    public static void VerifyNativeLoadSnapshotContent(SnapshotEnvelope before, SnapshotEnvelope after)
        => VerifySnapshotContentCore(before, after, nativeLoadInitialization: true);

    private static void VerifySnapshotContentCore(SnapshotEnvelope before, SnapshotEnvelope after, bool nativeLoadInitialization)
    {
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "saved-resource before");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(after, "saved-resource after");
        Require(before.SaveId.Value == after.SaveId.Value && before.PlayerId.Value == after.PlayerId.Value &&
            before.GameVersion == after.GameVersion, "snapshot actor/game identity differs.");
        RequireCoreFields(before, "before"); RequireCoreFields(after, "after");
        var comparison = nativeLoadInitialization ? NativeLoadComparisonView(before, after) : after;
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var beforeBoundary) &&
            FullShipmentRecoverySettlementBoundaryProjector.TryProject(comparison, out var afterBoundary) && beforeBoundary == afterBoundary,
            "persistent resources differ or cannot be projected: " + string.Join(",",
                FullShipmentRecoverySettlementBoundaryProjector.ChangedPersistentFields(before, comparison)));
    }

    private static void RequireCoreFields(SnapshotEnvelope snapshot, string boundary)
    {
        JsonElement Field(string domain, string name)
        {
            Require(snapshot.State.TryGetValue(domain, out var section) && section.ValueKind == JsonValueKind.Object &&
                section.TryGetProperty(name, out var envelope) && envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
                FieldEnvelopeValidator.IsReadableStatus(status.GetString()) && envelope.TryGetProperty("value", out var value) &&
                value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined,
                boundary + " required readable field " + domain + "." + name + " is unavailable.");
            return snapshot.State[domain].GetProperty(name).GetProperty("value");
        }
        foreach (var (domain, name) in new[] { ("player", "inventory"), ("farm", "crops") })
            Require(Field(domain, name).ValueKind == JsonValueKind.Array,
                boundary + " required field " + domain + "." + name + " must be an array.");
        foreach (var name in new[] { "total_days", "year", "day" })
        {
            var value = Field("time", name);
            Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                boundary + " required calendar field time." + name + " must be an integer.");
        }
        var season = Field("time", "season");
        Require(season.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(season.GetString()),
            boundary + " required calendar field time.season must be a nonempty string.");
        // The original projector owns currency and persistent-state hashing; reject null/unreadable inputs before invoking it.
        foreach (var name in new[] { "money", "club_coins", "shop_currency_balances", "total_money_earned" })
            Field("player", name);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Crop saved-resource readback " + reason);
    }
}
