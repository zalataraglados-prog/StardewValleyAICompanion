function Build-RuntimeSkillThresholdSource {
    param([string] $BootstrapDll, [string] $OutputRoot, [string] $RunId,
        [string] $PreferencePath, [int] $TargetScoringLevel, $Step)
    $requestPath = Join-Path $OutputRoot "skill-native-threshold-request.json"
    $receiptPath = Join-Path $OutputRoot "skill-native-threshold-receipt.json"
    $replayPath = Join-Path $OutputRoot "skill-native-threshold-replay.json"
    Write-JsonFile $requestPath ([ordered]@{
        schema_version = "skill_native_threshold_request.v1"
        run_id = $RunId
        preference_path = $PreferencePath
        action_queue_path = $Step.mechanical_compiled_queue_path
        execution_receipt_path = $Step.execution_path
        after_snapshot_path = $Step.after_snapshot_path
        target_scoring_level = $TargetScoringLevel
    })
    & dotnet $BootstrapDll build-skill-native-threshold-receipt --request $requestPath --output $receiptPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Skills native threshold source rejected the original action." }
    & dotnet $BootstrapDll verify-skill-native-threshold-receipt --receipt $receiptPath --output $replayPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Skills native threshold Verify rejected the original action." }
    if ((Get-FileHash -LiteralPath $receiptPath).Hash -cne (Get-FileHash -LiteralPath $replayPath).Hash) {
        throw "Skills native threshold build and Verify differ."
    }
    return $receiptPath
}

function Build-RuntimeSkillTeacherRow {
    param([string] $BootstrapDll, [string] $OutputRoot, [string] $RunId,
        [string] $NativeReceiptPath, [string] $ExecutionReceiptPath)
    $row = Join-Path $OutputRoot "skill-teacher-row.json"
    $replay = Join-Path $OutputRoot "skill-teacher-row-replay.json"
    & dotnet $BootstrapDll build-skill-teacher-row --native-receipt $NativeReceiptPath `
        --execution-receipt $ExecutionReceiptPath --trajectory-id ("skills.native." + $RunId) --output $row | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Skills Teacher row rejected the original native queue." }
    & dotnet $BootstrapDll verify-skill-teacher-row --row $row --output $replay | Out-Null
    if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $row).Hash -cne (Get-FileHash -LiteralPath $replay).Hash) {
        throw "Skills Teacher row and independent Verify differ."
    }
    return $row
}

function Initialize-RuntimeSkillTeacherQueue {
    param([string] $BootstrapDll, [string] $OutputRoot, [string] $SnapshotUrl,
        [int] $TargetScoringLevel)
    $source = Join-Path $OutputRoot "skill-teacher-before.json"
    $preference = Join-Path $OutputRoot "skill-teacher-preference.json"
    $plan = Join-Path $OutputRoot "skill-teacher-plan.json"
    $queue = Join-Path $OutputRoot "skill-teacher-queue.json"
    # Preserve the actual HTTP JSON, never a PowerShell-reserialized diagnostic copy.
    Invoke-WebRequest -Uri $SnapshotUrl -OutFile $source -TimeoutSec 180 | Out-Null
    & dotnet $BootstrapDll build-current-skill-teacher-preference --snapshot $source --output $preference | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Independent Skills comparison did not admit a unique choice." }
    $comparison = Get-Content -LiteralPath $preference -Raw | ConvertFrom-Json
    if ($comparison.deficit.scoringLevel -ne ($TargetScoringLevel - 1)) {
        throw "Loaded native Skills state differs from the declared incomplete threshold boundary."
    }
    & dotnet $BootstrapDll compile-current-skill-teacher-preference --preference $preference --plan-output $plan --output $queue | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Original Skills compiler rejected the selected independent comparison." }
    return [pscustomobject]@{ SnapshotPath = $source; PreferencePath = $preference; PlanPath = $plan; QueuePath = $queue }
}
