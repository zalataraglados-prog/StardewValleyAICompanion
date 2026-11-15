using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private const string SharedTransferSettlementRole = "shared-transfer-settlement";

    public Task SettleSharedTransferExecutionAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle) =>
        SettleSharedPortfolioExecutionAsync(operation, http, settle, cooking: false);

    public Task SettleSharedCookingExecutionAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle) =>
        SettleSharedPortfolioExecutionAsync(operation, http, settle, cooking: true);

    private async Task SettleSharedPortfolioExecutionAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle, bool cooking)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var scope = RequireClaimedSharedTransfer(operation, cooking);
            if (File.Exists(SharedSettlementReferencePath(operation.Record, cooking)))
            {
                scope.VerifyTransition(OriginalSupportingTransition(operation));
                return;
            }
            var request = ReadSharedSettlementRequest(scope);
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
    public Task RecoverSharedTransferSettlementAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        string evidenceDirectory, Func<string, string, Task<JsonObject>> post) =>
        RecoverSharedPortfolioSettlementAsync(operation, http, evidenceDirectory, post, cooking: false);

    public Task RecoverSharedCookingSettlementAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        string evidenceDirectory, Func<string, string, Task<JsonObject>> post) =>
        RecoverSharedPortfolioSettlementAsync(operation, http, evidenceDirectory, post, cooking: true);

    private async Task RecoverSharedPortfolioSettlementAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        string evidenceDirectory, Func<string, string, Task<JsonObject>> post, bool cooking)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var scope = RequireClaimedSharedTransfer(operation, cooking);
            if (File.Exists(SharedSettlementReferencePath(operation.Record, cooking)))
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
            var request = ReadSharedSettlementRequest(scope);
            var artifacts = new CropSeedRuntimeArtifacts(scope.InputHashes);
            var reply = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, options.BackendUrl, operation.RuntimeDirectory,
                output, artifacts, SharedSettlementRole(cooking), "settle-supporting-transition", request, post, write).ConfigureAwait(false);
            artifacts.Validate();
            await RetainSharedTransferSettlementAsync(operation, http, request, reply, output).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private CropTeacherSharedSourcePreparation RequireClaimedSharedTransfer(CropTeacherManagedSeedOperation operation, bool cooking)
    {
        RequireOperation(operation);
        if (operation.SharedSourceStart is not { } scope ||
            (cooking ? !scope.RequiresConsumptionSettlement || scope.RequiresTransferSettlement : !scope.RequiresTransferSettlement || scope.RequiresConsumptionSettlement) ||
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
        var cooking = scope.RequiresConsumptionSettlement;
        var expected = cooking ? scope.VerifyConsumptionSettlementReply(request, result) : scope.VerifyTransferSettlementReply(request, result);
        var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory,
            "settle-supporting-transition", request, JsonOptions);
        _ = await PortfolioMutationRequestJournal.ReadAsync(journal.JournalPath, journal.RequestPath, journal.Expected, journal.Hash).ConfigureAwait(false);
        var replyPath = Path.Combine(operation.RuntimeDirectory, SharedSettlementRole(cooking) + "-reply.json");
        if (!File.Exists(replyPath)) await write(replyPath, reply.ToJsonString(JsonOptions)).ConfigureAwait(false);
        if (!JsonNode.DeepEquals(reply, Read<JsonObject>(replyPath)))
            throw new InvalidDataException("Shared transfer settlement retained reply changed.");
        var final = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
            expected.SourceStateHash, JsonOptions, "Shared transfer settlement full readback").ConfigureAwait(false);
        CropTeacherSeedRuntimeTransaction.RequireLedger(expected, final.Ledger, "shared transfer settlement full readback");
        var reference = cooking
            ? JsonSerializer.Serialize(new CropTeacherSharedConsumptionSettlementReference(journal.RequestPath, replyPath,
                journal.JournalPath, options.BackendUrl, scope.ConsumptionPreparation!), JsonOptions)
            : JsonSerializer.Serialize(new CropTeacherSharedTransferSettlementReference(journal.RequestPath, replyPath, journal.JournalPath,
                options.BackendUrl) { Preparation = scope.TransferPreparation }, JsonOptions);
        await write(SharedSettlementReferencePath(operation.Record, cooking), reference).ConfigureAwait(false);
        scope.VerifyTransition(OriginalSupportingTransition(operation));
    }

    private static ReservationPortfolioSupportingTransitionSettlementRequest ReadSharedSettlementRequest(CropTeacherSharedSourcePreparation scope) =>
        scope.RequiresConsumptionSettlement ? scope.ReadConsumptionSettlementRequest() : scope.ReadTransferSettlementRequest();
    private static string SharedSettlementRole(bool cooking) => cooking ? "shared-consumption-settlement" : SharedTransferSettlementRole;
    private static string SharedSettlementReferencePath(JsonObject record, bool cooking) =>
        cooking ? SharedConsumptionReferencePath(record) : SharedTransferReferencePath(record);
}
