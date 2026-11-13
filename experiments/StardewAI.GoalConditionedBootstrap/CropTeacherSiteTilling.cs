using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

// Consumes one original compiled till; it never dispatches, claims production or
// commits the owner ledger. The existing supporting protocol owns those steps.
public sealed class CropTeacherSiteTilling : ICropTeacherSupportingTransition
{
    private readonly CropTeacherSiteTillCompilation preparation;
    private readonly CropTeacherRetainedInputs inputs;
    private readonly CropTeacherSupportingRebind rebind;
    private readonly string requestBlobPath;
    private readonly string requestBlobHash;

    internal CropTeacherSiteTilling(CropTeacherSiteTillCompilation preparation,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        this.preparation = preparation;
        preparation.ValidateInputsUnchanged();
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var outputs = new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Select(Path.GetFullPath).ToArray();
        Require(!comparer.Equals(outputs[0], outputs[1]) &&
            outputs.All(path => !preparation.InputHashes.ContainsKey(path)),
            "native till outputs alias a retained preparation role or lack absolute paths");
        var paths = preparation.InputHashes.Keys.Concat(outputs.Select(Path.GetFullPath)).ToArray();
        using var reads = InvocationReadSession.Start(paths);
        var original = new CropTeacherRetainedInputs(paths);
        var native = NativeExecutionActionSourceReader.ReadQueue(preparation.QueuePath, preparation.SourcePath,
            transition.ExecutionReceiptPath, transition.AfterSnapshotPath, transition.RunId, preparation.CandidateId);
        Require(native.Actions is [var action] && native.Queue.Items is [var item] &&
            item.OptionId == "executor.till_soil" && action.Request.OptionId == item.OptionId &&
            CropTeacherAppliedPrefixReceipt.Validate(native.Queue, native.Before, native.Receipt, native.After,
                transition, "till_soil", "Selected crop till") == 1,
            "native till is not its one original successful compiled action");
        var frame = native.Actions[0];
        CropTeacherNativeStepBoundary.Verify(frame.Before, frame.After, native.Before, native.Receipt.StepResults[0],
            preparation.TotalDay, "Selected crop till");
        _ = CropTeacherNativeStepPaths.Read(transition, native.Receipt, paths, "Selected crop till");
        var observation = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(preparation.ObservationPath, "selected till bounded inputs");
        Resources = NativeOutputObservationBoundary.ReadTillingResources(frame.Request, frame.Result,
            frame.Before, frame.After, observation, "Farm", preparation.Target,
            preparation.ToolSlotIndex, preparation.ToolQualifiedItemId);

        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(transition.ExecutionReceiptPath, "selected till raw receipt");
        var resultPath = Path.GetFullPath(raw.GetProperty("step_results")[0].GetProperty("execution_path").GetString()!);
        var result = CurrentTeacherFrontierSupport.Read<JsonElement>(resultPath, "selected till raw result");
        var journalPath = Path.GetFullPath(result.GetProperty("execution_request_journal_path").GetString()!);
        requestBlobPath = journalPath + ".gz";
        requestBlobHash = frame.SourceSha256["request_gzip"];
        var retained = new Dictionary<string, string>(original.ReadHashes(), comparer);
        foreach (var (label, path) in new[] { ("queue", preparation.QueuePath), ("result", resultPath),
                     ("before", frame.BeforeSnapshotPath), ("after", frame.AfterSnapshotPath), ("journal", journalPath) })
        {
            var hash = frame.SourceSha256[label];
            Require(!retained.TryGetValue(path, out var prior) || prior == hash,
                "native till reused an original input path with another hash");
            retained[path] = hash;
        }
        Require(!retained.ContainsKey(requestBlobPath), "compressed till request aliases a retained JSON role");
        inputs = new(retained.Keys.ToArray());
        Require(inputs.ReadHashes().All(pair => retained[pair.Key] == pair.Value), "native till inputs changed during retention");
        original.Validate();
        AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath);
        ValidateInputsUnchanged();
        rebind = new(native.After, preparation.BaseLedger, stepId, ValidateInputsUnchanged, "Selected crop till");
    }

    public NativeTillingResourceSummary Resources { get; }
    public string AfterSnapshotPath { get; }
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => rebind.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => rebind.Request;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => rebind.VerifyReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => inputs.ReadHashes()
        .Concat(new[] { new KeyValuePair<string, string>(requestBlobPath, requestBlobHash) })
        .ToDictionary(pair => pair.Key, pair => pair.Value, CropTeacherSeedTransferNativeOutcome.PathComparer);

    public void ValidateInputsUnchanged()
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        preparation.ValidateInputsUnchanged(); inputs.Validate();
        Require((File.GetAttributes(requestBlobPath) & FileAttributes.ReparsePoint) == 0 &&
            CurrentTeacherFrontierSupport.HashFile(requestBlobPath) == requestBlobHash,
            "native till compressed original request changed");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Selected crop till: " + reason + ".");
    }
}
