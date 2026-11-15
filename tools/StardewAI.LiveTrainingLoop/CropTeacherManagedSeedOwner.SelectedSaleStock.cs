using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // Re-materialize the ORIGINAL preview before re-binding only its operational preparation to the current head.
    public async Task<CropTeacherManagedSeedOperation> AdmitSelectedSaleStockPreparationAsync(CropTeacherSharedTransferDecision decision,
        string receiptPath, string afterSnapshotPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            var pins = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
            var selected = ReadSharedSourceDecision(decision.WithOutputs("", "", ""), pins);
            if (selected.OptionId != "inventory.transfer_item" || selected.ObjectiveContinuation is not null)
                throw new InvalidDataException("Selected sale stock preparation requires its original complete transfer intent.");
            var parameters = selected.RankedCandidate["parameters"]?.Deserialize<SmallModelActionParameter[]>(JsonOptions)
                ?? throw new InvalidDataException("Selected sale stock preview has no original parameters.");
            var markers = parameters.Where(row => row.Name == CropSaleStockTransferPreparation.RequestParameter).ToArray();
            if (markers.Length != 1 || !MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent) || intent is null)
                throw new InvalidDataException("Selected sale stock preview has no unique original preparation.");
            var originalLedgerPath = ReadSharedDecisionLedgerPath(decision);
            var originalSnapshot = pins.Read<SnapshotEnvelope>(decision.SnapshotPath);
            var originalLedger = pins.Read<StrategyCommitmentLedger>(originalLedgerPath);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(originalSnapshot,
                [new OptionAvailabilityCandidate { OptionId = selected.OptionId, AllowCropSaleStockPreparationPreview = true,
                    Parameters = MaterialTransferIntentBinder.ToParameters(intent) }], commitmentLedger: originalLedger);
            var original = availability.Options.SelectMany(row => row.EventCandidates)
                .Where(row => row.CandidateId == selected.CandidateId && row.Available).ToArray();
            if (original.Length != 1 || !SameTransferParameters(parameters, original[0].Parameters))
                throw new InvalidDataException("Selected sale stock preview differs from its original source/ledger materialization.");
            var originalRequest = JsonSerializer.Deserialize<ReservationPortfolioCommitRequest>(markers[0].Value, JsonOptions)
                ?? throw new InvalidDataException("Selected sale stock preview request is empty.");
            var source = pins.Read<SnapshotEnvelope>(headSnapshotPath);
            var ledger = pins.Read<StrategyCommitmentLedger>(headLedgerPath);
            var actual = CropSaleStockTransferPreparation.Create(source, ledger, MaterialTransferIntentBinder.ToParameters(intent), out var reasons);
            if (actual is null || actual.Request.GoalId != originalRequest.GoalId ||
                !JsonNode.DeepEquals(SelectedTransferIdentity(originalRequest.MaterialTransferPreparation!),
                    SelectedTransferIdentity(actual.Request.MaterialTransferPreparation!)))
                throw new InvalidDataException("Selected sale stock transfer changed its semantic intent/claims on the current source: " + string.Join(",", reasons));
            pins.Validate();
            return await AdmitSharedTransferPreparationCoreAsync(decision with { OriginalLedgerPath = originalLedgerPath }, actual.Request,
                receiptPath, afterSnapshotPath, runtimeDirectory).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private string ReadSharedDecisionLedgerPath(CropTeacherSharedTransferDecision decision)
    {
        if (decision.DecisionReference is null)
        {
            if (!SamePath(decision.SnapshotPath, headSnapshotPath))
                throw new InvalidDataException("Historical transfer preview requires its original retained daily decision.");
            return headLedgerPath;
        }
        var history = ReadAcceptedPlanningCertificates().ToArray();
        var retained = ReadDailyDecisionReference(decision.DecisionReference, history);
        if (!SamePath(retained.SourceSnapshotPath, decision.SnapshotPath) || !SamePath(retained.PlanPath, decision.PlanPath) ||
            !SamePath(retained.RankingPath, decision.RankingPath) ||
            !retained.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(decision.OwnedCandidateIds.OrderBy(id => id, StringComparer.Ordinal)))
            throw new InvalidDataException("Selected transfer changed its original retained daily ownership.");
        var value = ReadBoundPlanningRecord(decision.DecisionReference.Path, decision.DecisionReference.Sha256);
        var anchor = value["owner_anchor_sequence"]!.GetValue<int>();
        if (anchor == 0) return options.InitialLedgerPath;
        var certificate = history.Single(row => row.Sequence == anchor).Certificate;
        var path = StoredPath(certificate, "after_ledger_path");
        _ = ReadBoundPlanningRecord(path, Text(certificate, "after_ledger_sha256"));
        return path;
    }

    private static bool SameTransferParameters(SmallModelActionParameter[] left, SmallModelActionParameter[] right) =>
        left.Length == right.Length && left.GroupBy(row => row.Name, StringComparer.Ordinal).All(group => group.Count() == 1) &&
        left.OrderBy(row => row.Name, StringComparer.Ordinal).Select(row => (row.Name, row.Value)).SequenceEqual(
            right.OrderBy(row => row.Name, StringComparer.Ordinal).Select(row => (row.Name, row.Value)));

    private static JsonNode SelectedTransferIdentity(MaterialTransferPreparationRequest preparation) =>
        JsonSerializer.SerializeToNode(new
        {
            preparation.RouteSourceDecisionId, preparation.Intent,
            Claims = preparation.MaterialRelocations.OrderBy(row => row.MaterialReservationId, StringComparer.Ordinal).Select(row => new
            {
                row.MaterialReservationId, row.SourceNodeId, row.SourceSlotIndex, row.DestinationNodeId, row.QualifiedItemId, row.Quantity
            })
        }, JsonOptions)!; // Destination slot allocation is mechanical; selected source/intent/claims are not.
}
