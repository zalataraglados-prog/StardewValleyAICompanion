using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSharedSourceMarkerReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl,
    [property: JsonPropertyName("after_ledger_path")] string AfterLedgerPath);

public sealed partial class CropTeacherSharedSourceReturn
{
    private AcceptedMarker? marker;
    public bool ReleasesTransferLease { get; }
    public StrategyCommitmentLedger? CompletedLedger => marker?.LedgerCopy();

    public static CropTeacherSharedSourceReturn OpenAfterPreparedInventoryLeaseYield(string runRequestPath, string sourcePath,
        string ledgerPath, IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string stepId, CropTeacherSharedTransferSettlementReference transfer, string retainedReferencePath, bool product) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId, transfer, includePreparation: true,
            retainedReferencePath: retainedReferencePath, productTransfer: product, inventoryLease: true);

    public CropTeacherSharedSourceReturn BindAcceptedMarker(CropTeacherSharedSourceMarkerReference reference)
    {
        Require(marker is null, "Shared source already retains an accepted marker.");
        marker = new(this, reference);
        ValidateInputsUnchanged();
        return this;
    }

    private sealed class AcceptedMarker
    {
        private readonly CropTeacherRetainedInputs retained;
        private readonly StrategyCommitmentLedger ledger;
        internal AcceptedMarker(CropTeacherSharedSourceReturn scope, CropTeacherSharedSourceMarkerReference reference)
        {
            var paths = new[] { reference.RequestPath, reference.ReplyPath, reference.JournalPath, reference.AfterLedgerPath };
            Require(paths.All(Path.IsPathFullyQualified) &&
                paths.Select(Path.GetFullPath).Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == paths.Length &&
                paths.All(path => !scope.InputHashes.ContainsKey(Path.GetFullPath(path))),
                "Shared source marker aliases an original role or lacks absolute paths.");
            retained = new(paths);
            var (request, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioCommitRequest, ReservationPortfolioCommitResult>(
                reference.RequestPath, reference.ReplyPath, reference.JournalPath, reference.BackendUrl, "commit");
            Require(EqualJson(request, scope.RebindRequest) &&
                (scope.TransferSettlement is null || reference.BackendUrl == scope.TransferSettlement.BackendUrl),
                "Shared source marker differs from its original native-bound rebind/release.");
            ledger = scope.VerifyRebindReply(reply);
            Require(EqualJson(ledger, CurrentTeacherFrontierSupport.Read<StrategyCommitmentLedger>(reference.AfterLedgerPath,
                    "shared source original full marker readback")),
                "Shared source marker changed its full original ledger readback.");
            Validate();
        }
        internal IReadOnlyDictionary<string, string> Hashes => retained.ReadHashes();
        internal void Validate() => retained.Validate();
        internal StrategyCommitmentLedger LedgerCopy() { Validate(); return CropTeacherSeedSupportSource.Copy(ledger); }
    }
}
