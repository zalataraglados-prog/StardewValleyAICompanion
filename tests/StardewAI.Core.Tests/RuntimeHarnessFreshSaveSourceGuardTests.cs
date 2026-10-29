namespace StardewAI.Core.Tests;

public sealed class RuntimeHarnessFreshSaveSourceGuardTests
{
    [Fact]
    public void FreshSaveModeRequiresExplicitEmptyIsolatedTrainingRoot()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.FreshSave.cs");

        Assert.Contains("STARDEWAI_TRAINING_MODE", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TEST_SAVES", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("fresh-save-", source, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", source, StringComparison.Ordinal);
        Assert.Contains("Directory.EnumerateFileSystemEntries", source, StringComparison.Ordinal);
        Assert.Contains("fresh_save_and_existing_slot_are_mutually_exclusive", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSaveModeUsesCurrentNativeTitleFlowWithoutFabricatingSaveXml()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.FreshSave.cs");

        Assert.Contains("Game1.resetPlayer()", source, StringComparison.Ordinal);
        Assert.Contains("titleMenu.createdNewCharacter(skipIntro: true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveGame.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlSerializer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Write", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSaveSmokeRunsHiddenAndRequiresNativeSaveFiles()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeFreshSaveSmoke.ps1"));

        Assert.Contains("-WindowStyle Hidden", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TEST_CREATE_FRESH_SAVE", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("SaveGameInfo", source, StringComparison.Ordinal);
        Assert.Contains(
            "-TimeoutSeconds $StartupTimeoutSeconds",
            source,
            StringComparison.Ordinal);
        Assert.Contains("full_shipment_shipped_item_count = 0", source, StringComparison.Ordinal);
        Assert.Contains("[string] $OutputRoot = \"\"", source, StringComparison.Ordinal);
        Assert.Contains("$resolvedOutputRoot", source, StringComparison.Ordinal);
        Assert.Contains("$env:SMAPI_MODS_PATH = $smokeModsPath", source, StringComparison.Ordinal);
        Assert.Contains("loaded_mod_allowlist = $loadedModAllowlist", source, StringComparison.Ordinal);
        Assert.Contains("$harnessConfig.SlotName = \"\"", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.TransparentBridge", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.RuntimeTestHarness", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenWalnutCalibrationReusesNativeFreshSaveAndProductLoop()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-GoldenWalnutFreshSaveCalibration.ps1"));
        var calibration = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-ProductBootstrapCalibration.ps1"));
        var liveTrainingLoop = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));

        Assert.Contains(
            "Invoke-RuntimeFreshSaveSmoke.ps1",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "Invoke-ProductBootstrapCalibration.ps1",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-CaptureGoldenWalnutFreshSaveEvidence",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "[int] $RequiredVerifiedActions = 4",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-RequiredVerifiedActions $RequiredVerifiedActions",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-MinFreeSpaceMb $MinFreeSpaceMb",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-RequireGoldenWalnutSourceScheduleReady:$RequireSourceScheduleReady",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-StageOneCollectionRequirementInventory",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-StageOneCollectionAcquisitionLowering",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-MasterAnglerStageOneWindowIndex",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-RouteTimingCalibration",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "$effectiveMaxAttempts",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "$FreshSaveOutputRoot",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "fresh_save_output_root = $freshSaveOutputRoot",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "evidence_output_root = $resolvedOutputRoot",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string] $PrebuiltBinaryRoot = \"\"",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-PrebuiltBinaryRoot $PrebuiltBinaryRoot",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "prebuilt_binaries_reused =",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", workflow, StringComparison.Ordinal);

        Assert.Contains("--skip-training", calibration, StringComparison.Ordinal);
        Assert.Contains(
            "src\\StardewAI.Backend\\StardewAI.Backend.csproj",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "tools\\StardewAI.ProductExecutor\\StardewAI.ProductExecutor.csproj",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "tools\\StardewAI.LiveTrainingLoop\\StardewAI.LiveTrainingLoop.csproj",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet publish $entry.Project -c Release --nologo",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--artifacts-path $entry.ArtifactsDirectory",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "Join-Path $artifactDirectory \"binaries\"",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string] $PrebuiltBinaryRoot = \"\"",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "Prebuilt binary root was not found",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $reusePrebuiltBinaries)",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "prebuilt_binaries_reused = $reusePrebuiltBinaries",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "$backendDll = Join-Path $releaseEntrypoints[0].OutputDirectory",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--execution-snapshot-profile\", \"full",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--snapshot-artifact-mode\", \"content_addressed_gzip",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "if ($CaptureGoldenWalnutFreshSaveEvidence -or $resumeCropTeacher)",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "snapshot_artifact_mode =",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--capture-golden-walnut-fresh-save-evidence",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--require-golden-walnut-source-schedule-ready",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--stage-one-collection-requirement-inventory",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--stage-one-collection-acquisition-lowering",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--master-angler-stage-one-window-index",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--route-timing-calibration",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--require-native-save-boundary",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "if ($CaptureGoldenWalnutFreshSaveEvidence -or $resumeCropTeacher)",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--required-verified-actions\", $RequiredVerifiedActions",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--min-free-space-mb\", $MinFreeSpaceMb",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--max-unverified-execution-streak\", $MaxUnverifiedExecutionStreak",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "--max-queue-no-progress-streak\", $MaxQueueNoProgressStreak",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "-MaxUnverifiedExecutionStreak $MaxUnverifiedExecutionStreak",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "-MaxQueueNoProgressStreak $MaxQueueNoProgressStreak",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "--save-boundary-max-attempts\", $SaveBoundaryMaxAttempts",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "Golden Walnut fresh-save evidence capture produced no durable episode manifest.",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "Golden Walnut long-run milestone did not produce a verified source schedule.",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "-not $CaptureGoldenWalnutFreshSaveEvidence",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "Invoke-WebRequest -UseBasicParsing",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "[IO.File]::WriteAllText(",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "$initial = $initialJson | ConvertFrom-Json",
            calibration,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Write-Json (Join-Path $artifactDirectory \"initial-snapshot.json\")",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "golden_walnut_fresh_save_manifest_invalid message=",
            liveTrainingLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory",
            liveTrainingLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "ex is IOException or InvalidDataException or InvalidOperationException or",
            liveTrainingLoop,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StarterGiftSmokeReusesNativeFreshSaveAndNeverCreatesAFixture()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeStarterGiftSmoke.ps1"));

        Assert.Contains("Invoke-RuntimeFreshSaveSmoke.ps1", source, StringComparison.Ordinal);
        Assert.Contains("-KeepGameRunning", source, StringComparison.Ordinal);
        Assert.Contains("current_location.starter_gift_claim", source, StringComparison.Ordinal);
        Assert.Contains("rewards.claim_starter_gift", source, StringComparison.Ordinal);
        Assert.Contains("before-snapshot.json", source, StringComparison.Ordinal);
        Assert.Contains("after-snapshot.json", source, StringComparison.Ordinal);
        Assert.Contains("primitive_verification_status", source, StringComparison.Ordinal);
        Assert.Contains("Wait-StarterGiftProjection", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $game.Id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveGame.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialQueueAggregateRebindsTheWholeEpisodeStartBoundary()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs"));

        var aggregateStart = source.IndexOf(
            "var aggregate = JsonNode.Parse",
            StringComparison.Ordinal);
        var aggregateEnd = source.IndexOf(
            "aggregate[\"before_game_tick\"] = ReadLong(beforeSnapshot, \"game_tick\")",
            aggregateStart,
            StringComparison.Ordinal);
        Assert.True(aggregateStart >= 0 && aggregateEnd > aggregateStart);
        var aggregateBoundary = source[aggregateStart..aggregateEnd];
        Assert.Contains(
            "aggregate[\"effective_before_state_hash\"] = stateHash",
            aggregateBoundary,
            StringComparison.Ordinal);
        Assert.Contains(
            "aggregate[\"effective_before_snapshot_path\"] = beforeSnapshotPath",
            aggregateBoundary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenWalnutEvidenceArchivesExactSequentialStepBoundaries()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.Dataset.cs"));

        Assert.Contains(
            "execution[\"step_results\"] as JsonArray",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "appendResult.EpisodeId + \".item.\" + ordinal.ToString(\"D4\")",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"-item-\" + ordinal.ToString(\"D4\") + \".json\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildPlanExecutionEpisode(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "GoldenWalnutFreshSaveEpisodeArchive.Capture(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "stepEpisodePath,",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
