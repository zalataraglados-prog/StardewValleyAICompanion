using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private const string SharedTransferSettlementRole = "shared-transfer-settlement";

    public async Task SettleSharedTransferExecutionAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var scope = RequireClaimedSharedTransfer(operation);
            if (File.Exists(SharedTransferReferencePath(operation.Record)))
            {
                scope.VerifyTransition(OriginalSupportingTransition(operation));
                return;
            }
            var request = scope.ReadTransferSettlementRequest();
            var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory,
                "settle-supporting-transition", request, JsonOptions);
            if (File.Exists(journal.RequestPath) || File.Exists(journal.JournalPath))
                throw new InvalidOperationException("Shared transfer settlement was attempted; recover its original reply, never resend.");
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, operation.RuntimeDirectory,
                scope.BaseLedger.SourceStateHash, JsonOptions, "Shared transfer prepared authority").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(scope.BaseLedger, before.Ledger, "shared transfer prepared authority");
            var reply = await settle(new JsonObject(), request).ConfigureAwait(false);
            await RetainSharedTransferSettlementAsync(operation, http, request, reply, operation.RuntimeDirectory).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    // Explicit recovery only: original journal lookup/readback, no mutation or native retry.
    public async Task RecoverSharedTransferSettlementAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        string evidenceDirectory, Func<string, string, Task<JsonObject>> post)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var scope = RequireClaimedSharedTransfer(operation);
            if (File.Exists(SharedTransferReferencePath(operation.Record)))
            {
                scope.VerifyTransition(OriginalSupportingTransition(operation));
                return;
            }
            var output = Path.GetFullPath(evidenceDirectory);
            if (Directory.Exists(output) || File.Exists(output) || Within(operation.RuntimeDirectory, output) ||
                Within(output, operation.RuntimeDirectory) || scope.InputHashes.Keys.Any(path => Within(output, path)))
                throw new IOException("Shared transfer settlement recovery requires separate new evidence.");
            RequireDirectoryPath(output);
            Directory.CreateDirectory(output);
            var request = scope.ReadTransferSettlementRequest();
            var artifacts = new CropSeedRuntimeArtifacts(scope.InputHashes);
            var reply = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, options.BackendUrl, operation.RuntimeDirectory,
                output, artifacts, SharedTransferSettlementRole, "settle-supporting-transition", request, post, write).ConfigureAwait(false);
            artifacts.Validate();
            await RetainSharedTransferSettlementAsync(operation, http, request, reply, output).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private CropTeacherSharedSourcePreparation RequireClaimedSharedTransfer(CropTeacherManagedSeedOperation operation)
    {
        RequireOperation(operation);
        if (operation.SharedSourceStart is not { RequiresTransferSettlement: true } scope ||
            operation.Started is not null || operation.SupportingTransition is not null)
            throw new InvalidOperationException("Shared transfer settlement requires its original pending prepared yield.");
        _ = ReadOriginalSupportingClaim(operation, "Shared transfer");
        ValidateSupportingTransition(operation);
        return scope;
    }

    private async Task RetainSharedTransferSettlementAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        ReservationPortfolioSupportingTransitionSettlementRequest request, JsonObject reply, string output)
    {
        var scope = operation.SharedSourceStart!;
        var result = reply.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)
            ?? throw new InvalidDataException("Shared transfer settlement reply is empty.");
        var expected = scope.VerifyTransferSettlementReply(request, result);
        var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory,
            "settle-supporting-transition", request, JsonOptions);
        _ = await PortfolioMutationRequestJournal.ReadAsync(journal.JournalPath, journal.RequestPath, journal.Expected, journal.Hash).ConfigureAwait(false);
        var replyPath = Path.Combine(operation.RuntimeDirectory, SharedTransferSettlementRole + "-reply.json");
        if (!File.Exists(replyPath)) await write(replyPath, reply.ToJsonString(JsonOptions)).ConfigureAwait(false);
        if (!JsonNode.DeepEquals(reply, Read<JsonObject>(replyPath)))
            throw new InvalidDataException("Shared transfer settlement retained reply changed.");
        var final = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
            expected.SourceStateHash, JsonOptions, "Shared transfer settlement full readback").ConfigureAwait(false);
        CropTeacherSeedRuntimeTransaction.RequireLedger(expected, final.Ledger, "shared transfer settlement full readback");
        var reference = new CropTeacherSharedTransferSettlementReference(journal.RequestPath, replyPath, journal.JournalPath,
            options.BackendUrl) { Preparation = scope.TransferPreparation };
        await write(SharedTransferReferencePath(operation.Record), JsonSerializer.Serialize(reference, JsonOptions)).ConfigureAwait(false);
        scope.VerifyTransition(OriginalSupportingTransition(operation));
    }
}
