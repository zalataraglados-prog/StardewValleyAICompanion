namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeProductionSegmentBuilder
{
    private static MoneyRevenueCropProductionDecisionRequest
        VerifyTeacherDecision(MoneyCropNativeProductionInputManifest input)
    {
        var requestPath = VerifyArtifact(
            input.DecisionRequestPath,
            input.DecisionRequestSha256,
            "crop production Teacher decision request");
        VerifyArtifact(
            input.TeacherRequestPath,
            input.TeacherRequestSha256,
            "crop production Teacher request");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionDecisionRequest>(
                requestPath,
                "Money Revenue crop production Teacher decision");
        var rebuilt = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
            input.TeacherRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(persisted, rebuilt) &&
                persisted.SchemaVersion ==
                    "money_revenue_crop_production_decision_request.v2" &&
                persisted.DecisionId == input.DecisionId &&
                persisted.GoalId == input.GoalId &&
                persisted.TeacherSelectionPolicy ==
                    input.TeacherSelectionPolicy &&
                persisted.TeacherCandidateEvaluations.Length > 0 &&
                persisted.TeacherCandidateEvaluations.Count(value =>
                    value.Selected) == 1 &&
                !persisted.FormalProductTrainingAuthorized,
            "Money Revenue crop production Teacher decision did not rebuild exactly.");
        return persisted;
    }

    private static MoneyRevenueCropProductionDecisionReceipt
        VerifyDecisionReceipt(
            MoneyCropNativeProductionInputManifest input,
            MoneyRevenueCropProductionDecisionRequest decision)
    {
        var path = VerifyArtifact(
            input.DecisionReceiptPath,
            input.DecisionReceiptSha256,
            "crop production decision receipt");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionDecisionReceipt>(
                path,
                "Money Revenue crop production decision receipt");
        var rebuilt = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(
            input.DecisionRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(persisted, rebuilt) &&
                persisted.SchemaVersion ==
                    "money_revenue_crop_production_decision_receipt.v2" &&
                persisted.SourceStateHash == input.SourceStateHash &&
                persisted.DecisionId == decision.DecisionId &&
                persisted.TeacherRequestSha256 ==
                    input.TeacherRequestSha256 &&
                !persisted.FormalProductTrainingAuthorized,
            "Money Revenue crop production decision receipt did not rebuild exactly.");
        return persisted;
    }

    private static MoneyRevenueCropProductionObligationPlan
        VerifyProductionPlan(MoneyCropNativeProductionInputManifest input)
    {
        VerifyArtifact(
            input.CropProductionObligationRequestPath,
            input.CropProductionObligationRequestSha256,
            "crop production obligation request");
        var path = VerifyArtifact(
            input.CropProductionObligationPlanPath,
            input.CropProductionObligationPlanSha256,
            "crop production obligation plan");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                path,
                "Money Revenue crop production obligation plan");
        var rebuilt = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            input.CropProductionObligationRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(persisted, rebuilt) &&
                persisted.SourceStateHash == input.SourceStateHash &&
                persisted.DeadlineTotalDayExclusive ==
                    input.DeadlineTotalDayExclusive &&
                persisted.TargetTotalMoneyEarned ==
                    input.TargetTotalMoneyEarned &&
                persisted.Commitments.Length == input.CommitmentCount &&
                !persisted.FormalProductTrainingAuthorized,
            "Money Revenue crop production obligation plan did not rebuild exactly.");
        return persisted;
    }

    private static MoneyRevenueCropNativeProductionRunPlan VerifyRunPlan(
        MoneyCropNativeProductionRunSummary summary,
        MoneyCropNativeProductionInputManifest input,
        string runRoot)
    {
        VerifyArtifact(
            input.CropNativeRunPlanRequestPath,
            input.CropNativeRunPlanRequestSha256,
            "crop native-production run-plan request");
        var inputPlanPath = VerifyArtifact(
            input.CropNativeRunPlanPath,
            input.CropNativeRunPlanSha256,
            "crop native-production authoritative run plan");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeProductionRunPlan>(
                inputPlanPath,
                "Money Revenue crop native-production authoritative run plan");
        var rebuilt = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
            input.CropNativeRunPlanRequestPath);
        var executedPath = RequiredRunFile(
            summary.RunPlanPath,
            runRoot,
            "crop native-production executed run plan");
        var executed = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeProductionRunPlan>(
                executedPath,
                "Money Revenue crop native-production executed run plan");
        Require(CurrentTeacherFrontierSupport.HashFile(executedPath) ==
                    summary.RunPlanSha256 &&
                summary.RunPlanSha256 == input.CropNativeRunPlanSha256 &&
                BootstrapArtifactGuard.EqualJson(persisted, rebuilt) &&
                BootstrapArtifactGuard.EqualJson(executed, rebuilt) &&
                persisted.SourceStateHash == input.SourceStateHash &&
                persisted.SourceTotalDay == input.SourceTotalDay &&
                persisted.NativeProductionRunReady &&
                !persisted.FormalProductTrainingAuthorized,
            "Money Revenue crop native-production run plan did not rebuild exactly.");
        return persisted;
    }

    private static void VerifySourceAndRuntimeIsolation(
        MoneyCropNativeProductionRunSummary summary,
        MoneyCropNativeProductionInputManifest input,
        string runRoot)
    {
        var sourceSave = RequiredDirectory(
            summary.SourceSaveSlotPath,
            "crop native-production source save slot");
        Require(SamePath(sourceSave, input.SourceSaveSlotPath) &&
                summary.SourceSaveSha256Before ==
                    input.SourceSaveTreeSha256 &&
                summary.SourceSaveSha256After ==
                    input.SourceSaveTreeSha256 &&
                summary.IsolatedSaveSha256Before ==
                    input.SourceSaveTreeSha256 &&
                MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(
                    sourceSave) == input.SourceSaveTreeSha256,
            "Money Revenue crop native-production source save identity drifted.");
        RequiredRunFile(
            summary.SourcePreflightPath,
            Path.Combine(runRoot, "evidence"),
            "crop native-production source preflight");

        var modsRoot = RequiredRunDirectory(
            summary.IsolatedModsRootPath,
            runRoot,
            "crop native-production isolated Mods root");
        Require(SamePath(modsRoot, Path.Combine(runRoot, "smapi-mods")) &&
                summary.IsolatedModsSha256Before ==
                    summary.IsolatedModsSha256After &&
                MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(
                    modsRoot) == summary.IsolatedModsSha256Before &&
                summary.RuntimeModAllowlist.Length == 2,
            "Money Revenue crop native-production isolated Mods root drifted.");
        var expectedMods = new[]
        {
            "StardewAI.RuntimeTestHarness",
            "StardewAI.TransparentBridge"
        };
        for (var index = 0; index < expectedMods.Length; index++)
        {
            var observed = summary.RuntimeModAllowlist[index];
            var rebuilt = MoneyThresholdRuntimeModIsolationVerifier.Describe(
                modsRoot,
                expectedMods[index]);
            Require(observed == rebuilt,
                "Money Revenue crop native-production runtime Mod identity drifted: " +
                expectedMods[index]);
        }
    }

    private static void ValidateInput(
        MoneyCropNativeProductionInputManifest input)
    {
        Require(input.SchemaVersion ==
                    "money_crop_native_production_input_manifest.v1" &&
                input.Status == "native_crop_production_input_ready" &&
                !string.IsNullOrWhiteSpace(input.InputId) &&
                input.GoalId == "earn_money" &&
                input.DeadlineTotalDayExclusive == 224 &&
                input.TargetTotalMoneyEarned == 1_000_000 &&
                !string.IsNullOrWhiteSpace(input.SourceStateHash) &&
                input.SourceTotalDay >= 0 &&
                !string.IsNullOrWhiteSpace(input.DecisionId) &&
                input.TeacherSelectionPolicy ==
                    MoneyRevenueCropProductionTeacherDecisionBuilder
                        .SelectionPolicy &&
                input.CommitmentCount > 0 &&
                input.MaxQueueItemCount is >= 1 and <= 8 &&
                !input.FormalProductTrainingAuthorized,
            "Money Revenue crop native-production input manifest is invalid.");
        VerifyArtifact(
            input.StrategyLedgerPath,
            input.StrategyLedgerSha256,
            "crop production strategy ledger");
    }

    private static MoneyRevenueArtifactBinding[] BuildArtifacts(
        string summaryPath,
        string inputPath,
        MoneyCropNativeProductionInputManifest input,
        string checkpointPath,
        MoneyRevenueCropProductionProofCheckpoint checkpoint,
        string recurrenceManifestPath,
        string recurrenceReceiptPath,
        string finalSnapshotPath,
        string finalSnapshotSha256,
        string isolatedSavePath,
        string isolatedSaveSha256)
    {
        var values = new List<MoneyRevenueArtifactBinding>
        {
            Artifact("run_summary", summaryPath),
            Artifact("production_input_manifest", inputPath),
            Artifact("teacher_request", input.TeacherRequestPath),
            Artifact("teacher_decision", input.DecisionRequestPath),
            Artifact("decision_receipt", input.DecisionReceiptPath),
            Artifact("source_snapshot", input.SnapshotPath),
            Artifact("strategy_ledger", input.StrategyLedgerPath),
            Artifact("production_obligation_request",
                input.CropProductionObligationRequestPath),
            Artifact("production_obligation_plan",
                input.CropProductionObligationPlanPath),
            Artifact("native_run_plan_request",
                input.CropNativeRunPlanRequestPath),
            Artifact("native_run_plan", input.CropNativeRunPlanPath),
            Artifact("proof_checkpoint_request", checkpoint.RequestPath),
            Artifact("proof_fragment_index", checkpoint.FragmentIndexPath),
            Artifact("proof_checkpoint", checkpointPath),
            Artifact("revenue_recurrence_manifest", recurrenceManifestPath),
            Artifact("revenue_recurrence_receipt", recurrenceReceiptPath),
            new("final_snapshot", finalSnapshotPath, finalSnapshotSha256),
            new("final_isolated_save_tree", isolatedSavePath,
                isolatedSaveSha256)
        };
        Require(values.All(value => IsSha256(value.Sha256)) &&
                values.Select(value => value.Role)
                    .Distinct(StringComparer.Ordinal).Count() == values.Count,
            "Money Revenue crop native-production artifact bindings are invalid.");
        return values.ToArray();
    }

    private static MoneyRevenueArtifactBinding Artifact(
        string role,
        string path) => new(
            role,
            RequiredFile(path, role),
            CurrentTeacherFrontierSupport.HashFile(path));

    private static string VerifyArtifact(
        string path,
        string expectedSha256,
        string label)
    {
        var fullPath = RequiredFile(path, label);
        Require(IsSha256(expectedSha256) &&
                CurrentTeacherFrontierSupport.HashFile(fullPath) ==
                    expectedSha256,
            label + " hash drifted.");
        return fullPath;
    }

    private static string RequiredRunFile(
        string path,
        string expectedRoot,
        string label)
    {
        var fullPath = RequiredFile(path, label);
        Require(IsStrictDescendant(fullPath, expectedRoot),
            label + " is outside its run root.");
        return fullPath;
    }

    private static string RequiredRunDirectory(
        string path,
        string expectedRoot,
        string label)
    {
        var fullPath = RequiredDirectory(path, label);
        Require(IsStrictDescendant(fullPath, expectedRoot),
            label + " is outside its run root.");
        return fullPath;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static string RequiredDirectory(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException(
                label + " is missing: " + fullPath);
    }

    private static bool IsStrictDescendant(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(
            fullRoot + Path.DirectorySeparatorChar,
            PathComparison);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            PathComparison);

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
