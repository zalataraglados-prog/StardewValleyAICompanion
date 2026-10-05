function Resolve-RuntimeEvidenceInputPath {
    param(
        [Parameter(Mandatory)] [string] $ProjectRoot,
        [Parameter(Mandatory)] [string] $Path
    )
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Write-Utf8Text {
    param([string] $Path, [string] $Value)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Write-JsonFile {
    param([string] $Path, $Value)
    Write-Utf8Text -Path $Path -Value (
        $Value | ConvertTo-Json -Depth 96)
}

function Get-DirectoryContentHash {
    param([string] $Path)
    $root = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $rows = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Sort-Object FullName | ForEach-Object {
            if (-not $_.FullName.StartsWith(
                    $root + '\',
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "File escaped hash root: $($_.FullName)"
            }
            $relative = $_.FullName.Substring($root.Length + 1)
            $hash = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash.ToLowerInvariant()
            "$relative`t$($_.Length)`t$hash"
        })
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes([string]::Join("`n", $rows))
        return [BitConverter]::ToString(
            $algorithm.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Read-StateValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                return $response.Content | ConvertFrom-Json
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Get-FreshRuntimeSnapshot {
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -TimeoutSec 45
            $snapshot = $response.Content | ConvertFrom-Json
            $location = [string](Read-StateValue `
                $snapshot "player" "location_id")
            if ($response.StatusCode -eq 200 -and
                -not [string]::IsNullOrWhiteSpace($location)) {
                return [pscustomobject]@{
                    Raw = $response.Content
                    Value = $snapshot
                }
            }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a fresh world snapshot. Last error: $lastError"
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 240)
    $json = if ($Body -is [string]) {
        $Body
    }
    else {
        $Body | ConvertTo-Json -Depth 96
    }
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri $Url `
            -ContentType "application/json; charset=utf-8" -Body $json `
            -TimeoutSec $TimeoutSeconds
    }
    catch {
        $detail = ""
        if ($null -ne $_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = [IO.StreamReader]::new($stream)
                try { $detail = $reader.ReadToEnd() }
                finally { $reader.Dispose() }
            }
        }
        throw "POST $Url failed: $($_.Exception.Message) $detail"
    }
    return [pscustomobject]@{
        Raw = $response.Content
        Value = $response.Content | ConvertFrom-Json
    }
}

function Invoke-RuntimeBootstrap {
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string[]] $Arguments
    )
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell turns native stderr into ErrorRecord instances.
        # Capture it before the caller's Stop preference truncates JSON errors.
        $ErrorActionPreference = "Continue"
        $nativeOutput = @(& dotnet $BootstrapDll @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        $detail = @($nativeOutput |
            ForEach-Object { [string]$_ } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Last 20) -join "`n"
        throw "Goal-conditioned bootstrap failed: $($Arguments[0]) " +
            "(exit $exitCode). $detail"
    }
}

function Save-RuntimeSnapshotAndIngest {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] $Capture,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile
    )
    Write-Utf8Text -Path $Path -Value $Capture.Raw
    $ingest = Invoke-JsonPost `
        -Url "$BackendUrl/api/v1/snapshots?profile=$SnapshotProfile" `
        -Body $Capture.Raw
    if (-not [bool]$ingest.Value.accepted -or
        [string]$ingest.Value.state_hash -ne
            [string]$Capture.Value.state_hash) {
        throw "Backend rejected or changed snapshot identity for $Path."
    }
}

function Invoke-RuntimeRanking {
    param(
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $GoalId,
        [Parameter(Mandatory)] [string] $SnapshotStateHash,
        [Parameter(Mandatory)] [string] $OutputPath,
        [Parameter(Mandatory)] [string] $OptionId,
        [object[]] $Parameters = @()
    )
    [object[]]$candidates = @()
    if ($Parameters.Count -gt 0) {
        $candidates = @([ordered]@{
            option_id = $OptionId
            parameters = $Parameters
            explicit_confirmation_granted = $false
            invocation_source = 0
            actor_is_host = $true
            ownership_authorized = $true
            adapter_id = "vanilla_native"
        })
    }
    [string[]]$candidateOptionIds = if ($candidates.Count -eq 0) {
        @($OptionId)
    }
    else { @() }
    $request = [ordered]@{
        goal_id = $GoalId
        execution_mode = "training_singleplayer"
        state_hash = $SnapshotStateHash
        candidate_option_ids = $candidateOptionIds
        candidates = $candidates
        include_blocked_options = $false
        training_report = [ordered]@{}
        policy_checkpoint_path = $null
        require_structured_policy = $false
    }
    $response = Invoke-JsonPost `
        -Url "$BackendUrl/api/v1/planner/baseline/rank-options" `
        -Body $request
    Write-Utf8Text -Path $OutputPath -Value $response.Raw
    if ([string]$response.Value.schema_version -ne
        "availability_policy_prediction.v1") {
        throw "Unexpected ranking schema for $OptionId."
    }
    return $response.Value
}

function New-RuntimeQueueContext {
    param(
        [Parameter(Mandatory)] [string] $LoopDll,
        [Parameter(Mandatory)] [string] $ArtifactDirectory,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $ExecutorRoot,
        [Parameter(Mandatory)] [string] $ProductUrl,
        [Parameter(Mandatory)] [string] $RunId,
        [Parameter(Mandatory)] [string] $IsolatedSavesPath
    )
    return [pscustomobject]@{
        LoopDll = $LoopDll
        ArtifactDirectory = $ArtifactDirectory
        BackendUrl = $BackendUrl
        SnapshotUrl = $SnapshotUrl
        SnapshotProfile = $SnapshotProfile
        ExecutorRoot = $ExecutorRoot
        ProductUrl = $ProductUrl
        RunId = $RunId
        IsolatedSavesPath = $IsolatedSavesPath
    }
}

function Invoke-RuntimeDailyPlanStep {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $OptionId,
        [Parameter(Mandatory)] [string] $CandidateKind,
        [Parameter(Mandatory)] [string] $CandidateId,
        [string[]] $CandidateParameters = @()
    )
    $stepRoot = Join-Path $Context.ArtifactDirectory $Name
    $capture = Get-FreshRuntimeSnapshot `
        -Url $Context.SnapshotUrl -TimeoutSeconds 60
    $sourcePath = Join-Path $stepRoot "source-snapshot.json"
    Write-Utf8Text -Path $sourcePath -Value $capture.Raw
    $arguments = [Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $Context.LoopDll,
        "--root", $stepRoot,
        "--backend-url", $Context.BackendUrl,
        "--bridge-snapshot-url", $Context.SnapshotUrl,
        "--execution-snapshot-profile", $Context.SnapshotProfile,
        "--executor-url", $Context.ExecutorRoot,
        "--snapshot-file", $sourcePath,
        "--no-manifest",
        "--skip-training",
        "--run-id", $Context.RunId,
        "--save-isolation-path", $Context.IsolatedSavesPath,
        "--iterations", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", $OptionId,
        "--daily-plan-candidate-kind", $CandidateKind,
        "--daily-plan-candidate-id", $CandidateId,
        "--emit-queue-execution-receipt",
        "--after-snapshot-wait-ms", "1000",
        "--after-snapshot-poll-ms", "250",
        "--continue-after-blocked-queue-items"
    )) { $arguments.Add([string]$value) }
    foreach ($parameter in $CandidateParameters) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add($parameter)
    }
    $stdout = & dotnet $arguments
    $stdout | Set-Content -LiteralPath (Join-Path $stepRoot "loop.stdout.log") `
        -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Daily plan step $Name failed with exit $LASTEXITCODE."
    }
    return Read-RuntimeLoopArtifacts `
        -LoopRoot $stepRoot -RunId $Context.RunId
}

function Invoke-RuntimePrecompiledQueue {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $QueuePath,
        [Parameter(Mandatory)] [string] $BeforePath
    )
    $loopRoot = Join-Path $Context.ArtifactDirectory $Name
    $arguments = @(
        $Context.LoopDll,
        "--root", $loopRoot,
        "--backend-url", $Context.BackendUrl,
        "--bridge-snapshot-url", $Context.SnapshotUrl,
        "--execution-snapshot-profile", $Context.SnapshotProfile,
        "--snapshot-file", $BeforePath,
        "--executor-url", $Context.ProductUrl,
        "--use-product-executor",
        "--no-manifest",
        "--skip-training",
        "--run-id", $Context.RunId,
        "--save-isolation-path", $Context.IsolatedSavesPath,
        "--max-attempts", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--precompiled-queue", $QueuePath,
        "--sleep-ms", "0",
        "--after-snapshot-wait-ms", "1000",
        "--after-snapshot-poll-ms", "250"
    )
    $stdout = & dotnet $arguments
    $stdout | Set-Content `
        -LiteralPath (Join-Path $loopRoot "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Precompiled queue $Name failed with exit $LASTEXITCODE."
    }
    return Read-RuntimeLoopArtifacts `
        -LoopRoot $loopRoot -RunId $Context.RunId
}

function Invoke-RuntimeTeacherPreferenceQueue {
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [string] $PreferencePath,
        [Parameter(Mandatory)] [string] $BeforePath
    )
    $loopRoot = Join-Path $Context.ArtifactDirectory $Name
    $arguments = @(
        $Context.LoopDll,
        "--root", $loopRoot,
        "--backend-url", $Context.BackendUrl,
        "--bridge-snapshot-url", $Context.SnapshotUrl,
        "--execution-snapshot-profile", $Context.SnapshotProfile,
        "--snapshot-file", $BeforePath,
        "--executor-url", $Context.ProductUrl,
        "--use-product-executor",
        "--no-manifest",
        "--skip-training",
        "--run-id", $Context.RunId,
        "--save-isolation-path", $Context.IsolatedSavesPath,
        "--max-attempts", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--teacher-preference", $PreferencePath,
        "--sleep-ms", "0",
        "--after-snapshot-wait-ms", "1000",
        "--after-snapshot-poll-ms", "250"
    )
    $stdout = & dotnet $arguments
    $stdout | Set-Content `
        -LiteralPath (Join-Path $loopRoot "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Teacher preference queue $Name failed with exit $LASTEXITCODE."
    }
    return Read-RuntimeLoopArtifacts `
        -LoopRoot $loopRoot -RunId $Context.RunId
}

function Read-RuntimeLoopArtifacts {
    param(
        [Parameter(Mandatory)] [string] $LoopRoot,
        [Parameter(Mandatory)] [string] $RunId
    )
    $root = Join-Path $LoopRoot "runs\$RunId\live-snapshots"
    $queuePath = Join-Path $root "compiled-queue-0001.json"
    $beforePath = Join-Path $root "before-snapshot-0001.json"
    $executionPath = Join-Path $root "execution-0001.json"
    $afterPath = Join-Path $root "after-snapshot-0001.json"
    foreach ($path in @($queuePath, $beforePath, $executionPath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing loop artifact: $path"
        }
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw |
        ConvertFrom-Json
    if ([string]$execution.status -ne "applied" -or
        -not [bool]$execution.after_snapshot_fresh) {
        throw "Loop execution was not applied with a fresh after snapshot."
    }
    return [pscustomobject]@{
        QueuePath = $queuePath
        BeforePath = $beforePath
        ExecutionPath = $executionPath
        AfterPath = $afterPath
        Queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
        Before = Get-Content -LiteralPath $beforePath -Raw | ConvertFrom-Json
        Execution = $execution
        After = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
    }
}

function Assert-RuntimePortsUnused {
    param(
        [Parameter(Mandatory)] [int[]] $Ports,
        [string] $OperationName = "Runtime evidence"
    )
    foreach ($port in $Ports) {
        if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port `
                -ErrorAction SilentlyContinue)) {
            throw "$OperationName requires unused port $port."
        }
    }
}

function Save-RuntimeProcessEnvironment {
    param([Parameter(Mandatory)] [string[]] $Names)
    $saved = @{}
    foreach ($name in $Names) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    return $saved
}

function Restore-RuntimeProcessEnvironment {
    param([Parameter(Mandatory)] $Values)
    foreach ($name in $Values.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $Values[$name],
            "Process")
    }
}

function Start-RuntimeEvidenceProcess {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [string[]] $ArgumentList = @(),
        [Parameter(Mandatory)] [string] $WorkingDirectory,
        [Parameter(Mandatory)] [string] $StandardOutputPath,
        [Parameter(Mandatory)] [string] $StandardErrorPath
    )
    $start = @{
        FilePath = $FilePath
        WorkingDirectory = $WorkingDirectory
        WindowStyle = "Hidden"
        RedirectStandardOutput = $StandardOutputPath
        RedirectStandardError = $StandardErrorPath
        PassThru = $true
    }
    if ($ArgumentList.Count -gt 0) {
        $start.ArgumentList = $ArgumentList
    }
    return Start-Process @start
}

function Stop-RuntimeEvidenceProcesses {
    param([object[]] $Processes)
    foreach ($process in $Processes) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(10000) | Out-Null
        }
    }
}
