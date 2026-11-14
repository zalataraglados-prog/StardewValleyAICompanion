using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// An observed shared-source return, not authority to execute foreign work or credit crop production.
public sealed class CropTeacherSharedSourceReturn : ICropTeacherSupportingTransition
{
    private readonly CropTeacherRetainedInputs inputs;
    private readonly Dictionary<string, string> requestBlobs = new(CropTeacherSeedTransferNativeOutcome.PathComparer);
    private readonly CropTeacherSupportingRebind rebind;

    private CropTeacherSharedSourceReturn(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { runRequestPath, sourcePath, ledgerPath, transition.QueuePath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath };
        Require(paths.All(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)) &&
                !string.IsNullOrWhiteSpace(transition.BeforeSnapshotPath) && Path.IsPathFullyQualified(transition.BeforeSnapshotPath),
            "Shared source return requires absolute original artifact paths.");
        paths = paths.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(comparer).Count() == paths.Length &&
                comparer.Equals(paths[1], Path.GetFullPath(transition.BeforeSnapshotPath)),
            "Shared source return aliases an original role or has another source.");
        using var reads = InvocationReadSession.Start(paths);
        var original = new CropTeacherRetainedInputs(paths);
        var source = CropTeacherSupportingSource.Read(paths[0], paths[1], paths[2], commitmentIds, stepId);
        Require(transition.ExecutorVersion == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor &&
                transition.TotalDay == MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(source.Snapshot, "time", "total_days"),
            "Shared source return differs from the original executor/day boundary.");
        var native = NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue(paths[3], paths[1], paths[4], paths[5],
            transition.RunId, transition.SelectedCandidateId);
        Require(native.Actions.Length > 0 && native.Before.StateHash == source.Snapshot.StateHash,
            "Shared source return has no original native source chain.");
        for (var index = 0; index < native.Actions.Length; index++)
            CropTeacherNativeStepBoundary.Verify(native.Actions[index].Before, native.Actions[index].After, source.Snapshot,
                native.Receipt.StepResults[index], transition.TotalDay, "Shared source return");

        // Unsettled reserved consumption belongs to its original owner, never to this marker-only return.
        var projectedLedger = CropTeacherSeedSupportSource.Copy(source.Ledger);
        projectedLedger.SourceStateHash = native.After.StateHash;
        _ = CropTeacherOwnedSeedSupply.Read(native.After, projectedLedger, commitmentIds);

        var retained = new Dictionary<string, string>(original.ReadHashes(), comparer);
        void Retain(string path, string expected)
        {
            path = Path.GetFullPath(path);
            Require(!retained.TryGetValue(path, out var prior) || prior == expected,
                "Shared source return changed a retained native artifact.");
            retained[path] = expected;
        }
        foreach (var (label, path) in new[] { ("queue", paths[3]), ("before", paths[1]), ("aggregate", paths[4]), ("after", paths[5]) })
            Retain(path, native.SourceSha256[label]);
        var rawReceipt = CurrentTeacherFrontierSupport.Read<JsonElement>(paths[4], "shared source native receipt");
        for (var index = 0; index < native.Actions.Length; index++)
        {
            var frame = native.Actions[index];
            var resultPath = CurrentTeacherFrontierSupport.RequiredString(rawReceipt.GetProperty("step_results")[index], "execution_path");
            var result = CurrentTeacherFrontierSupport.Read<JsonElement>(resultPath, "shared source native result");
            var journalPath = Path.GetFullPath(CurrentTeacherFrontierSupport.RequiredString(result, "execution_request_journal_path"));
            foreach (var (label, path) in new[] { ("before", frame.BeforeSnapshotPath), ("after", frame.AfterSnapshotPath),
                         ("result", resultPath), ("journal", journalPath) })
                Retain(path, frame.SourceSha256[label]);
            var blob = journalPath + ".gz";
            Require(!requestBlobs.TryGetValue(blob, out var previous) || previous == frame.SourceSha256["request_gzip"],
                "Shared source return reused an original request path with another hash.");
            requestBlobs[blob] = frame.SourceSha256["request_gzip"];
        }
        Require(requestBlobs.Keys.All(path => !retained.ContainsKey(path)),
            "Shared source return request blob aliases a JSON artifact.");
        inputs = new(retained.Keys.ToArray());
        Require(inputs.ReadHashes().All(pair => retained[pair.Key] == pair.Value),
            "Shared source return native artifacts changed during retention.");
        original.Validate();
        SourceStateHash = source.Snapshot.StateHash;
        AfterStateHash = native.After.StateHash;
        AfterSnapshotPath = paths[5];
        VerifiedNativeStepCount = native.Actions.Length;
        ValidateInputsUnchanged();
        rebind = new(native.After, source.Ledger, stepId, ValidateInputsUnchanged, "Shared source return");
    }

    public static CropTeacherSharedSourceReturn Open(string runRequestPath, string sourcePath, string ledgerPath,
        IReadOnlyCollection<string> commitmentIds, MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId) =>
        new(runRequestPath, sourcePath, ledgerPath, commitmentIds, transition, stepId);

    public string SourceStateHash { get; }
    public string AfterStateHash { get; }
    public string AfterSnapshotPath { get; }
    public int VerifiedNativeStepCount { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool CropProductionCreditAuthorized => false;
    public bool TeacherPreferenceLabelEligible => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes().Concat(requestBlobs)
        .ToDictionary(pair => pair.Key, pair => pair.Value, CropTeacherSeedTransferNativeOutcome.PathComparer);

    public void ValidateInputsUnchanged()
    {
        inputs.Validate();
        foreach (var (path, hash) in requestBlobs)
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && CurrentTeacherFrontierSupport.HashFile(path) == hash,
                "Shared source return original compressed request changed.");
    }
}
