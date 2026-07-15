param(
    [string] $ExecutorBaseUrl = "http://127.0.0.1:8767",
    [string] $RunId = $env:STARDEWAI_TRAINING_RUN_ID,
    [string] $SaveIsolationPath = $env:STARDEWAI_SAVE_ISOLATION_PATH,
    [string] $QueueId = "runtime-sleep-smoke",
    [string] $QueueItemId = "runtime-sleep-smoke.item.1",
    [string] $BeforeStateHash = "runtime-sleep-smoke.before"
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body
    )

    $json = $Body | ConvertTo-Json -Depth 16
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Pass -RunId or set STARDEWAI_TRAINING_RUN_ID."
}

if ([string]::IsNullOrWhiteSpace($SaveIsolationPath)) {
    throw "SaveIsolationPath is required. Pass -SaveIsolationPath or set STARDEWAI_SAVE_ISOLATION_PATH."
}

$health = Invoke-RestMethod -Method Get -Uri "$ExecutorBaseUrl/health" -Headers @{ "Accept" = "application/json" }
if ($health.status -ne "ok") {
    throw "Executor health check failed."
}

$request = [ordered]@{
    schema_version = "training_execution_request.v1"
    run_id = $RunId
    queue_id = $QueueId
    queue_item_id = $QueueItemId
    before_state_hash = $BeforeStateHash
    option_id = "executor.sleep"
    execution_mode = "training_singleplayer"
    actor = "training_farmer.main"
    save_isolation_path = $SaveIsolationPath
    request_nonce = [guid]::NewGuid().ToString("N")
    created_at = [DateTimeOffset]::UtcNow.ToString("O")
    max_crops = 512
}

$result = Invoke-JsonPost "$ExecutorBaseUrl/api/v1/training/execute" $request
$result | ConvertTo-Json -Depth 32

if ($result.status -notin @("applied", "blocked")) {
    throw "Unexpected executor status '$($result.status)'."
}

if ($result.primitive_kind -ne "sleep") {
    throw "Unexpected primitive kind '$($result.primitive_kind)'."
}

if ($result.status -eq "applied" -and $result.primitive_verification_status -ne "verified") {
    throw "Applied sleep result was not verified."
}

if ($result.status -eq "blocked" -and @($result.block_reasons).Count -eq 0) {
    throw "Blocked sleep result did not include block_reasons."
}
