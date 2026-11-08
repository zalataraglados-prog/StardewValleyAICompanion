using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    internal async Task<StrategyLedgerEvidence> CompleteWithoutNativeDispatchAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        if (Interlocked.Exchange(ref completionStarted, 1) != 0)
            throw new InvalidOperationException("Crop seed completion was already attempted; recover its original journal.");
        Exception? failure = null;
        try
        {
            var artifacts = executionGuard is null ? null : new CropSeedRuntimeArtifacts(scope.InputHashes);
            if (artifacts is not null)
                foreach (var path in new[] { transition.QueuePath, transition.ExecutionReceiptPath, transition.BeforeSnapshotPath, transition.AfterSnapshotPath })
                    artifacts.Pin(path);
            await ValidateNoDispatchContextAsync(options, scope, marker.Ledger, preparedQueue, ValidateDispatchQueueCore, transition);
            var transitionPath = Path.Combine(options.EvidenceDirectory, "controller-stop-transition.json");
            if (artifacts is not null)
            {
                RequireNoNativeStopArtifacts(options.EvidenceDirectory);
                artifacts.Validate();
                await write(transitionPath, JsonSerializer.Serialize(transition, JsonOptions));
                artifacts.Pin(transitionPath);
            }
            var final = await ReadOriginalAuthorityAsync();
            RequireLedger(marker.Ledger, final.Ledger, "no-dispatch readback");
            diagnostic["status"] = "crop_seed_support_prepared_no_native_dispatch";
            diagnostic["native_request_count"] = 0;
            diagnostic["controller_stop_receipt_path"] = Path.GetFullPath(transition.ExecutionReceiptPath);
            diagnostic["final_ledger_path"] = final.Path;
            if (artifacts is not null)
            {
                artifacts.Pin(final.Path);
                var prepared = ReadPreparedBoundary();
                void Validate()
                {
                    prepared.Validate();
                    artifacts.Validate();
                    RequireNoNativeStopArtifacts(options.EvidenceDirectory);
                }
                var detached = new StrategyLedgerEvidence(final.Path,
                    JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(final.Ledger, JsonOptions), JsonOptions)!);
                Validate();
                verifiedStopBoundary = new(prepared, detached, scope.SnapshotPath, transitionPath,
                    Path.GetFullPath(transition.ExecutionReceiptPath), Validate);
            }
            return final;
        }
        catch (Exception error) { failure = error; diagnostic["status"] = "crop_seed_support_no_dispatch_unverified"; diagnostic["error"] = error.Message; throw; }
        finally { await WriteSummaryAsync(write, Path.Combine(options.EvidenceDirectory, "completion-summary.json"), diagnostic, failure); }
    }

    internal static void RequireNoNativeStopArtifacts(string root)
    {
        if (new[] { "native-transition-request.json", "support-proof.json", "settlement-reply.json", "acquisition-reply.json" }
            .Any(name => File.Exists(Path.Combine(root, name))))
            throw new InvalidDataException("Original zero-dispatch stop conflicts with retained native effects.");
    }
}
