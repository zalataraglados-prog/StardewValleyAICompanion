using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerTeacherNativeJoinBuilder
{
    public static MasterAnglerTeacherNativeJoin Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MasterAnglerTeacherNativeJoinRequest>(requestPath, "master_angler_teacher_native_join_request.v1");
        var rowPath = RequiredFile(request.TeacherRowPath);
        var nativePath = RequiredFile(request.NativeCatchReceiptPath);
        var row = ReadTyped<PolicyDecisionTrajectoryEnvelope>(rowPath, PolicyTrajectoryVersionPins.TrajectorySchema);
        var envelopeRejection = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(envelopeRejection is null, "Master Angler Teacher envelope rejected: " + envelopeRejection);
        var teacher = row.Audit?.TeacherSupervision ?? throw new InvalidDataException("Master Angler independent Teacher sources are missing.");
        var rejection = new PolicyPairwiseSupervisionAdmission(new CollectionTeacherSupervisionReplayVerifier()).Validate(row, out var pairs);
        Require(rejection is null && pairs.Length > 0, "Master Angler independent Teacher rejected: " + (rejection ?? "strict_comparison_missing"));
        var native = MasterAnglerNativeCatchReceiptBuilder.Verify(nativePath);
        var nativeRequest = ReadTyped<MasterAnglerNativeCatchRequest>(RequiredFile(native.RequestPath), "master_angler_native_catch_request.v1");
        var nativeQueue = ReadTyped<ActionQueueEnvelope>(RequiredFile(nativeRequest.ActionQueuePath), "action_queue.v1");
        var sources = teacher.Sources!;
        var preference = ReadTyped<CurrentStageOneCollectionTeacherPreferenceLabel>(RequiredFile(sources.PreferencePath),
            "current_stage_one_collection_teacher_preference.v1");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["teacher_row"] = CurrentTeacherFrontierSupport.HashFile(rowPath),
            ["native_receipt"] = CurrentTeacherFrontierSupport.HashFile(nativePath)
        };
        foreach (var source in new[]
        {
            ("requirement_inventory", sources.RequirementInventoryPath), ("acquisition_lowering", sources.AcquisitionLoweringPath),
            ("ranking", sources.RankingPath), ("before_snapshot", sources.BeforeSnapshotPath),
            ("target_date_intents", sources.TargetDateIntentsPath), ("preference", sources.PreferencePath),
            ("execution_receipt", sources.ExecutionReceiptPath), ("after_snapshot", sources.AfterSnapshotPath)
        }) hashes["teacher." + source.Item1] = CurrentTeacherFrontierSupport.HashFile(RequiredFile(source.Item2));
        if (!string.IsNullOrWhiteSpace(sources.StrategyLedgerPath))
            hashes["teacher.strategy_ledger"] = CurrentTeacherFrontierSupport.HashFile(RequiredFile(sources.StrategyLedgerPath));
        foreach (var source in native.SourceSha256) hashes["native." + source.Key] = source.Value;
        ValidateBinding(native, row, preference, nativeQueue, hashes);
        return new(requestPath, hashes, native.RunId, native.SaveId, native.PlayerId, native.TotalDay, row.TrajectoryId,
            row.Context!.SplitKey, row.Context.DatasetPartition, row.Selection!.CandidateId,
            teacher.CandidateDenominatorSha256, pairs.Length, native.QualifiedItemId, native.OptionId,
            native.QueueId, native.QueueItemId, native.RequestNonce, native.BeforeStateHash, native.AfterStateHash,
            native.BeforeGameTick, native.AfterGameTick);
    }

    public static MasterAnglerTeacherNativeJoin Verify(string receiptPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(receiptPath), "master_angler_teacher_native_join.v1");
        var rebuilt = Build(CurrentTeacherFrontierSupport.RequiredString(raw, "request_path"));
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Master Angler Teacher/native join differs from its independently replayed original sources.");
        return rebuilt;
    }

    // Each verifier stays authoritative; these checks only connect their original decision and capture.
    private static void ValidateBinding(MasterAnglerNativeCatchReceipt native, PolicyDecisionTrajectoryEnvelope row,
        CurrentStageOneCollectionTeacherPreferenceLabel preference, ActionQueueEnvelope nativeQueue,
        IReadOnlyDictionary<string, string> hashes)
    {
        var teacher = row.Audit.TeacherSupervision!;
        var selected = preference.SelectedCandidate;
        Require(native.RunId == row.RunId && native.SaveId == row.Context.SaveId &&
            native.BeforeStateHash == row.SourceStateHash && row.Versions.Executor == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor &&
            row.Context.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey),
            "Master Angler Teacher/native decision identity or original partition drifted.");
        foreach (var pair in new[] { ("before_snapshot", "before"), ("after_snapshot", "after"),
            ("requirement_inventory", "requirement_inventory"), ("acquisition_lowering", "acquisition_lowering") })
            Require(hashes.TryGetValue("teacher." + pair.Item1, out var left) && !string.IsNullOrWhiteSpace(left) &&
                native.SourceSha256.TryGetValue(pair.Item2, out var right) && left == right,
                "Master Angler Teacher/native original source mismatch: " + pair.Item1);
        Require(selected is not null && row.Selection.CandidateId == selected.CandidateId &&
            teacher.SelectedCandidateId == selected.CandidateId && row.Selection.OptionId == selected.OptionId &&
            row.Outcome.QueueId == native.QueueId && row.Outcome.PrimitiveOptionId == native.OptionId &&
            row.Outcome.Status == "applied" && row.Outcome.Success && row.Outcome.AfterSnapshotFresh &&
            row.Outcome.ActualTicks == native.AfterGameTick - native.BeforeGameTick &&
            teacher.SelectedQueueItemIds.Contains(native.QueueItemId, StringComparer.Ordinal) &&
            preference.CompiledQueue?.QueueId == native.QueueId && nativeQueue.QueueId == native.QueueId &&
            EqualJson(preference.CompiledQueue, nativeQueue) && nativeQueue.Items.Count(item =>
                item.QueueItemId == native.QueueItemId && item.OptionId == native.OptionId) == 1,
            "Master Angler native capture is not the original independently compiled Teacher action.");
        Require(selected!.RequirementCredits.Any(credit => credit.RequirementSetId == "master_angler" &&
                credit.QualifiedItemId == native.QualifiedItemId) &&
            teacher.RequirementTransitions.Any(transition => transition.RequirementSetId == "master_angler" &&
                transition.QualifiedItemId == native.QualifiedItemId && transition.TransitionKind == "native_collection_false_to_true" &&
                transition.Verified && transition.BeforeValue == "False" && transition.AfterValue == "True"),
            "Master Angler native catch is not the independently selected collection transition.");
    }
}
