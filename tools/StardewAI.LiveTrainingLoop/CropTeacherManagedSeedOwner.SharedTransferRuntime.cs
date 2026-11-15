using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherSharedSourceSelection> PrepareSharedTransferAsync(CropTeacherManagedSeedOperation operation,
        HttpClient http, Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<StrategyCommitmentLedger, string, Task<CropTeacherSharedSourceSelection>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var support = RequireUnpreparedSharedTransfer(operation);
            var root = StoredPath(operation.Record, "preparation_runtime_directory");
            var request = support.PreparationRequest;
            var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory, "commit", request, JsonOptions);
            if (Directory.Exists(root) || File.Exists(root) || File.Exists(journal.RequestPath) || File.Exists(journal.JournalPath))
                throw new InvalidOperationException("Shared preparation has retained state; recover its original reply, never resend.");
            Directory.CreateDirectory(root);
            var reference = new CropTeacherSharedTransferPreparationReference(journal.RequestPath,
                Path.Combine(root, "marker-reply.json"), journal.JournalPath, options.BackendUrl);
            CropTeacherSharedSourceSelection selection = null!;
            StrategyCommitmentLedger preparedLedger = null!;
            var prepared = await PortfolioPreparationRuntime.RunAsync(http,
                new(options.BackendUrl, root, options.RunId, options.ExecutorVersion), request,
                CreateSharedTransferPreparationIntent(operation.Record), () => { }, () => support.BaseLedger,
                reply => preparedLedger = support.VerifyPreparationReply(reply), support.ValidateInputsUnchanged,
                async () =>
                {
                    selection = await CompileSharedTransferAsync(operation, preparedLedger, reference, compileOriginal).ConfigureAwait(false);
                    return selection.QueuePath;
                }, path => ValidateSharedTransferSelection(operation, selection, reference, path), commit, write,
                "Shared transfer support", "shared_transfer_support").ConfigureAwait(false);
            await PersistSharedTransferPreparedAsync(operation, selection, reference, prepared.IntentSha256).ConfigureAwait(false);
            return selection;
        }
        finally { gate.Release(); }
    }

    // The original journal/lookup and full readback recover accepted preparation; no commit or native retry.
    public async Task<CropTeacherSharedSourceSelection> RecoverSharedTransferPreparationAsync(CropTeacherManagedSeedOperation operation,
        HttpClient http, Func<string, string, Task<JsonObject>> post,
        Func<StrategyCommitmentLedger, string, Task<CropTeacherSharedSourceSelection>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var support = RequireUnpreparedSharedTransfer(operation);
            var root = StoredPath(operation.Record, "preparation_runtime_directory");
            var intentPath = Path.Combine(root, "support-intent.json");
            if (HashFile(intentPath) != Text(operation.Record, "original_intent_sha256"))
                throw new InvalidDataException("Shared preparation recovery lost its original intent.");
            var output = Path.Combine(root, "recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            var artifacts = new CropSeedRuntimeArtifacts(support.InputHashes);
            artifacts.Pin(intentPath);
            var request = support.PreparationRequest;
            var reply = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, options.BackendUrl, root, output,
                artifacts, "marker", "commit", request, post, write).ConfigureAwait(false);
            var ledger = support.VerifyPreparationReply(reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Shared preparation recovery reply is empty."));
            var replyPath = Path.Combine(root, "marker-reply.json");
            if (!File.Exists(replyPath)) await write(replyPath, reply.ToJsonString(JsonOptions)).ConfigureAwait(false);
            artifacts.Pin(replyPath);
            var readback = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
                ledger.SourceStateHash, JsonOptions, "Shared preparation recovered authority").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(ledger, readback.Ledger, "shared preparation recovered authority");
            var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory, "commit", request, JsonOptions);
            var reference = new CropTeacherSharedTransferPreparationReference(journal.RequestPath, replyPath, journal.JournalPath, options.BackendUrl);
            artifacts.Validate();
            var selection = await CompileSharedTransferAsync(operation, ledger, reference, compileOriginal).ConfigureAwait(false);
            await PersistSharedTransferPreparedAsync(operation, selection, reference, artifacts.Hashes[intentPath]).ConfigureAwait(false);
            return selection;
        }
        finally { gate.Release(); }
    }

    private CropTeacherSharedTransferSupport RequireUnpreparedSharedTransfer(CropTeacherManagedSeedOperation operation)
    {
        RequireOperation(operation);
        if (operation.SharedTransferSupport is not { } support || operation.Prepared is not null ||
            operation.SharedSourceStart is not null || operation.Started is not null || File.Exists(NativeSupportingPath(operation, "claim")))
            throw new InvalidOperationException("Shared preparation requires its original unique unprepared owner admission.");
        return support;
    }

    private async Task<CropTeacherSharedSourceSelection> CompileSharedTransferAsync(CropTeacherManagedSeedOperation operation,
        StrategyCommitmentLedger ledger, CropTeacherSharedTransferPreparationReference reference,
        Func<StrategyCommitmentLedger, string, Task<CropTeacherSharedSourceSelection>> compileOriginal)
    {
        var output = Path.Combine(StoredPath(operation.Record, "preparation_runtime_directory"), "compile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var selection = await compileOriginal(ledger, output).ConfigureAwait(false);
        if (new[] { selection.ModelPlanPath, selection.QueuePath, selection.EvidencePath }.Any(path =>
            !Path.IsPathFullyQualified(path) || !Within(output, path) || SamePath(output, path)))
            throw new InvalidDataException("Fresh shared preparation outputs must stay in their owned compilation directory.");
        selection = selection with { MaterialTransferPreparation = reference };
        _ = ValidateSharedTransferSelection(operation, selection, reference, selection.QueuePath);
        return selection;
    }

    private string ValidateSharedTransferSelection(CropTeacherManagedSeedOperation operation, CropTeacherSharedSourceSelection selection,
        CropTeacherSharedTransferPreparationReference reference, string queuePath)
    {
        var decision = operation.Record["shared_transfer_decision"]!.Deserialize<CropTeacherSharedTransferDecision>(JsonOptions)!;
        var expected = decision.WithOutputs(selection.ModelPlanPath, selection.QueuePath, selection.EvidencePath) with { MaterialTransferPreparation = reference };
        if (!SamePath(queuePath, selection.QueuePath) || !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, JsonOptions),
            JsonSerializer.SerializeToNode(selection, JsonOptions)))
            throw new InvalidDataException("Shared preparation changed its original selected decision.");
        var record = SharedPreparedExecutionRecord(operation.Record, selection, reference);
        var start = OpenSharedSourceStart(record, operation.SharedTransferSupport!.PreparationRequest.GoalId);
        start.ValidateInputsUnchanged();
        return HashFile(queuePath);
    }
}
