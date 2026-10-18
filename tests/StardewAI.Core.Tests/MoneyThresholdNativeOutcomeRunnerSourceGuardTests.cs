namespace StardewAI.Core.Tests;

public sealed class MoneyThresholdNativeOutcomeRunnerSourceGuardTests
{
    private static readonly string Source = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMoneyThresholdNativeOutcome.ps1"));
    private static readonly string BatchSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Update-MoneyThresholdNativeOutcomeBatch.ps1"));
    private static readonly string InitializationSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyThresholdProductionWorkPlan.ps1"));
    private static readonly string ComparisonPlanSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyRevenueComparisonWork.ps1"));
    private static readonly string ComparisonCompletionSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Complete-MoneyRevenueComparisonWork.ps1"));
    private static readonly string TerminalProofCollectionSource =
        File.ReadAllText(
            FindRepositoryFile(
                "scripts",
                "Initialize-MoneyRevenueTerminalProofCollection.ps1"));

    [Fact]
    public void RunnerUsesImmutableSourceSaveAndAnIsolatedClone()
    {
        Assert.Contains("Get-DirectoryContentHash -Path $sourceSave", Source,
            StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSave", Source,
            StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES = $isolatedSaves", Source,
            StringComparison.Ordinal);
        Assert.Contains("$env:SMAPI_MODS_PATH = $isolatedMods", Source,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.TransparentBridge\"", Source,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.RuntimeTestHarness\"", Source,
            StringComparison.Ordinal);
        Assert.Contains("Get-MoneyRuntimeModAllowlist", Source,
            StringComparison.Ordinal);
        Assert.Contains("isolated_mods_root_path = $isolatedMods", Source,
            StringComparison.Ordinal);
        Assert.Contains("runtime_mod_allowlist = $runtimeModAllowlist", Source,
            StringComparison.Ordinal);
        Assert.Contains("money_threshold_native_outcome_run_summary.v2", Source,
            StringComparison.Ordinal);
        Assert.Contains("-TimeoutSeconds $StartupTimeoutSeconds", Source,
            StringComparison.Ordinal);
        Assert.Contains("$initialSnapshotUrl = \"$snapshotUrl&fresh=true\"",
            Source, StringComparison.Ordinal);
        Assert.Contains("-Url $initialSnapshotUrl", Source,
            StringComparison.Ordinal);
        Assert.Contains("source_save_unchanged", Source,
            StringComparison.Ordinal);
        Assert.Contains("Stop-RuntimeEvidenceProcesses", Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerUsesProductionSelectionAndNativeVerificationOnly()
    {
        var dependencyGate =
            "Assert-MoneyDependencyExpansionReady -Path $dependencyPath";
        var firstRuntimeSideEffect =
            "New-Item -ItemType Directory -Force -Path $isolatedSaves";

        Assert.Contains("build-money-revenue-candidate-denominator", Source,
            StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-native-outcome-selection", Source,
            StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-teacher-corpus", Source,
            StringComparison.Ordinal);
        Assert.Contains("--skip-training", Source, StringComparison.Ordinal);
        Assert.Contains("$selection.objective_parameters", Source,
            StringComparison.Ordinal);
        Assert.Contains("Wait-NativeGameTime", Source,
            StringComparison.Ordinal);
        Assert.Contains("effective_open_time", Source,
            StringComparison.Ordinal);
        Assert.Contains("goal_method_dependency_expansions.v1", Source,
            StringComparison.Ordinal);
        Assert.Contains(dependencyGate, Source, StringComparison.Ordinal);
        Assert.True(
            Source.IndexOf(dependencyGate, StringComparison.Ordinal) <
            Source.IndexOf(firstRuntimeSideEffect, StringComparison.Ordinal));

        Assert.DoesNotContain("debug.advance_time_to", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("debug.setup_sale_target", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-RuntimeSaleMainlineSmoke", Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BatchReplaysRunsAndRebuildsTheCanonicalWorkPlan()
    {
        Assert.Contains("build-money-threshold-production-package",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("--work-plan-request", BatchSource,
            StringComparison.Ordinal);
        Assert.Contains("--work-plan-output", BatchSource,
            StringComparison.Ordinal);
        Assert.Contains("money_threshold_coverage_request_manifest.v1",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("$CoverageRequestTemplate",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("run_summary_paths = $summaryPaths",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_threshold_native_outcome_batch_request.v4",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_threshold_native_outcome_run_summary.v2",
            BatchSource + ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "money_threshold_native_outcome_run_summary.v1",
            Source + BatchSource + ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.Contains("comparison_admissions = $comparisonAdmissions",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_threshold_comparison_admission_index.v3",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized = $false",
            BatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", BatchSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", BatchSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            BatchSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyInitializationCannotDowngradeCanonicalMoneyEvidence()
    {
        const string corpusGuard =
            "Test-Path -LiteralPath $canonicalCorpusPath -PathType Leaf";
        const string workPlanGuard =
            "[int]$existingWorkPlan.native_outcome_complete_cell_count -ne 0";
        const string firstWrite = "Write-JsonFile -Path $requestPath";

        Assert.Contains("money-threshold-native-outcome-corpus.json",
            InitializationSource, StringComparison.Ordinal);
        Assert.Contains(corpusGuard, InitializationSource,
            StringComparison.Ordinal);
        Assert.Contains(workPlanGuard, InitializationSource,
            StringComparison.Ordinal);
        Assert.True(
            InitializationSource.IndexOf(corpusGuard, StringComparison.Ordinal) <
            InitializationSource.IndexOf(firstWrite, StringComparison.Ordinal));
        Assert.True(
            InitializationSource.IndexOf(workPlanGuard, StringComparison.Ordinal) <
            InitializationSource.IndexOf(firstWrite, StringComparison.Ordinal));
    }

    [Fact]
    public void ComparisonInitializationOnlyBuildsVerifiedPlanningArtifacts()
    {
        Assert.Contains("build-money-revenue-alternative-denominator",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-comparison-collection-plan",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-terminal-rebind-manifest",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-terminal-candidate-rebind",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-concrete-alternative-denominator",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-counterfactual-proof-requests",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-terminal-proof-collection-plan",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_terminal_proof_collection_plan_request.v2",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("run_id = [string]$plan.run_id",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("dataset_partition = [string]$plan.dataset_partition",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("source_strategy_ledger_path = $sourceLedgerPath",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("money_revenue_acquisition_rollout_index.v1",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("native_run_summary_path = $summaryPath",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized = $false",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("training/execute", ComparisonPlanSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComparisonCompletionBuildsAndJoinsSelectedNativeTrajectory()
    {
        Assert.Contains("build-money-revenue-terminal-execution-manifest",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-terminal-execution-proof",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-comparison-admission",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-native-trajectory",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-teacher-corpus",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("native_trajectory_receipt_path = $trajectoryReceiptPath",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.Contains("money_threshold_comparison_admission_index.v3",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("outcome_not_supported_by_money_corpus",
            ComparisonCompletionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", ComparisonCompletionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", ComparisonCompletionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("training/execute", ComparisonCompletionSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalProofCollectionMaterializesOnlyVerifiedOfflineReuse()
    {
        Assert.Contains("reuse_verified_shop_sale_episode",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.Contains("build-strategy-ledger-boundary-capture",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-recurrence-proof",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-terminal-proof-collection-checkpoint",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-terminal-proof-index",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_terminal_proof_collection_plan.v2",
            TerminalProofCollectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "-Path ([string]$request.terminal_snapshot_path)",
            TerminalProofCollectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "([string]$item.terminal_snapshot_sha256).ToLowerInvariant()",
            TerminalProofCollectionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "-Path ([string]$episode.before_snapshot_path)",
            TerminalProofCollectionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("training/execute",
            TerminalProofCollectionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            TerminalProofCollectionSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
