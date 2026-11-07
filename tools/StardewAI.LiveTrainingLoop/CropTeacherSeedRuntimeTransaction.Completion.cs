using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    public Task<StrategyLedgerEvidence> CompleteAsync(string queuePath, string receiptPath,
        string afterSnapshotPath, string selectedCandidateId) => CompleteAsync(CreateTransition(queuePath, receiptPath, afterSnapshotPath, selectedCandidateId));

    internal MoneyRevenueCropNativeLifecycleTransitionRequest CreateTransition(string queuePath, string receiptPath,
        string afterSnapshotPath, string selectedCandidateId) => new(
            scope.TransitionKind, scope.TotalDay, queuePath, scope.SnapshotPath, receiptPath,
            afterSnapshotPath, options.RunId, options.ExecutorVersion, selectedCandidateId);

    internal void ValidateTransitionIdentity(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        if (transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TotalDay != scope.TotalDay)
            throw new InvalidDataException("Crop seed runtime native receipt identity differs from preparation.");
    }

    public async Task<StrategyLedgerEvidence> CompleteAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        if (Interlocked.Exchange(ref completionStarted, 1) != 0)
            throw new InvalidOperationException("Crop seed support completion was already attempted; recover original journals instead of replaying.");
        Exception? failure = null;
        try
        {
            ValidateTransitionIdentity(transition);
            var nativeQueueHash = ValidateDispatchQueueCore(transition.QueuePath);
            await write(Path.Combine(options.EvidenceDirectory, "native-transition-request.json"),
                JsonSerializer.Serialize(transition, JsonOptions));
            var verified = marker.VerifyTransition(transition, Path.Combine(options.EvidenceDirectory, "support-proof.json"), nativeQueueHash);
            diagnostic["support_proof_path"] = verified.ProofPath;
            diagnostic["after_snapshot_path"] = transition.AfterSnapshotPath;
            diagnostic["after_state_hash"] = verified.AfterStateHash;
            diagnostic["support_slice_verified"] = true;
            var settlementDiagnostic = new JsonObject();
            diagnostic["settlement"] = settlementDiagnostic;
            var reply = await settle(settlementDiagnostic, verified.ReadSettlementRequest());
            await write(Path.Combine(options.EvidenceDirectory, "settlement-reply.json"), reply.ToJsonString(JsonOptions));
            var result = reply.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)
                ?? throw new InvalidDataException("Crop seed runtime settlement reply is empty.");
            var custody = verified.VerifySettlementReply(result);
            diagnostic["resource_settlement_verified"] = true;
            var expected = custody.Ledger;
            if (verified.RequiresAcquisition)
            {
                var acquisitionDiagnostic = new JsonObject();
                diagnostic["acquisition"] = acquisitionDiagnostic;
                var acquiredReply = await commit(acquisitionDiagnostic, custody.ReadAcquiredSeedRequest!());
                await write(Path.Combine(options.EvidenceDirectory, "acquisition-reply.json"), acquiredReply.ToJsonString(JsonOptions));
                var acquired = acquiredReply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                    ?? throw new InvalidDataException("Crop seed runtime acquisition reply is empty.");
                expected = custody.VerifyAcquiredSeedReply!(acquired);
                diagnostic["seed_custody_verified"] = true;
            }
            var final = await ReadLedgerAsync(http, options, expected.SourceStateHash, "Crop seed support final readback");
            RequireLedger(expected, final.Ledger, "final readback");
            diagnostic["final_ledger_path"] = final.Path;
            diagnostic["final_ledger_revision"] = final.Ledger.Revision;
            diagnostic["status"] = "crop_seed_support_roundtrip_verified";
            return final;
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "crop_seed_support_completion_incomplete";
            diagnostic["error"] = error.Message;
            throw;
        }
        finally
        {
            await WriteSummaryAsync(write, Path.Combine(options.EvidenceDirectory, "completion-summary.json"), diagnostic, failure);
        }
    }
}
