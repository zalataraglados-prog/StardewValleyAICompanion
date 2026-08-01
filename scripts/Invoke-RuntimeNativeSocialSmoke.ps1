param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-native-social-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-native-social-smoke",
    [int] $BackendPort = 5158,
    [int] $StartupTimeoutSeconds = 120,
    [switch] $ProductionRouteOnly,
    [switch] $ProductionPursuitOnly,
    [switch] $ProductionGiftPursuitOnly,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

if (@($ProductionRouteOnly, $ProductionPursuitOnly, $ProductionGiftPursuitOnly).Where({ $_ }).Count -gt 1) {
    throw "ProductionRouteOnly, ProductionPursuitOnly, and ProductionGiftPursuitOnly are mutually exclusive."
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $Value | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] $Body,
        [int] $TimeoutSeconds = 120
    )

    $json = $Body | ConvertTo-Json -Depth 32
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $json -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 3
            if ($response.status -eq "ok" -or $response.schema_version -eq "snapshot.v1") {
                return $response
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $requiredDomains = @("player", "time", "menus", "options", "farm", "current_location", "locations", "npcs", "quests", "world_progress", "mods", "modded_state", "fishing", "mining")

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec 5
            $locationReadable = $false
            $domainCheck = ""
            if ($null -ne $snapshot.state -and
                $snapshot.state.PSObject.Properties.Name -contains "player" -and
                $snapshot.state.player.PSObject.Properties.Name -contains "location_id") {
                $locationReadable = $snapshot.state.player.location_id.status -in @("available", "derived")
            }

            $missingDomains = @()
            if ($null -ne $snapshot.state) {
                foreach ($domain in $requiredDomains) {
                    if (-not ($snapshot.state.PSObject.Properties.Name -contains $domain)) {
                        $missingDomains += $domain
                    }
                }
            }
            else {
                $missingDomains = $requiredDomains
            }

            $lastStatus = "location_id_readable=$locationReadable;missing_domains=$($missingDomains -join ',');completeness=$($snapshot.completeness)"
            if ($locationReadable -and $missingDomains.Count -eq 0) {
                return $snapshot
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for world-ready snapshot. Last status: $lastStatus"
}

function Read-FieldValue {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field
    )

    if ($null -eq $Snapshot.state) {
        return $null
    }

    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) {
        return $null
    }

    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) {
        return $null
    }

    return $fieldNode.value
}

function Get-SnapshotInt {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field,
        [int] $Default = 0
    )

    $value = Read-FieldValue $Snapshot $Domain $Field
    if ($null -eq $value) {
        return $Default
    }
    $intValue = 0
    if ([int]::TryParse([string]$value, [ref]$intValue)) {
        return $intValue
    }
    return $Default
}

function Get-SnapshotString {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field,
        [string] $Default = ""
    )

    $value = Read-FieldValue $Snapshot $Domain $Field
    if ($null -eq $value) {
        return $Default
    }
    return [string]$value
}

function Get-SnapshotObject {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field
    )

    if ($null -eq $Snapshot.state) { return $null }
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Get-SnapshotBool {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $Domain,
        [Parameter(Mandatory = $true)] [string] $Field,
        [bool] $Default = $false
    )

    $value = Read-FieldValue $Snapshot $Domain $Field
    if ($null -eq $value) {
        return $Default
    }
    if ($value -is [bool]) {
        return $value
    }
    return [bool]::Parse([string]$value)
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ "Accept" = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-RawJsonGet {
    param([Parameter(Mandatory = $true)] [string] $Url)
    $client = [System.Net.WebClient]::new()
    try {
        $client.Headers.Set("Accept", "application/json")
        $client.DownloadString($Url)
    }
    finally {
        $client.Dispose()
    }
}

function Invoke-RawJsonPost {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [string] $Json
    )
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body $Json
}

function Invoke-RawJsonPostStrict {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [string] $Json,
        [string] $ErrorArtifactPath = ""
    )
    try {
        Invoke-RawJsonPost -Url $Url -Json $Json
    }
    catch {
        $errInfo = [ordered]@{
            url = $Url
            status_code = 0
            response_body = ""
            exception_message = $_.Exception.Message
        }
        $httpResponse = $_.Exception.Response
        if ($null -ne $httpResponse) {
            $errInfo.status_code = [int]$httpResponse.StatusCode
            try {
                $rs = $httpResponse.GetResponseStream()
                if ($null -ne $rs) {
                    $r = New-Object System.IO.StreamReader($rs)
                    $errInfo.response_body = $r.ReadToEnd()
                    $r.Dispose()
                }
            }
            catch {
                $errInfo.response_body_read_error = $_.Exception.Message
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($ErrorArtifactPath)) {
            Write-JsonFile -Path $ErrorArtifactPath -Value $errInfo
        }
        throw "HTTP POST $Url : status=$($errInfo.status_code) body=$($errInfo.response_body) msg=$($errInfo.exception_message)"
    }
}

function Get-ParameterValue {
    param(
        [Parameter(Mandatory = $true)] $Parameters,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    if ($null -eq $Parameters) { throw "Parameter array is null while looking for '$Name'" }
    $matches = @($Parameters | Where-Object { $_.name -eq $Name })
    if ($matches.Count -eq 0) { throw "Required parameter '$Name' not found in parameters array" }
    if ($matches.Count -gt 1) { throw "Duplicate parameter '$Name' in parameters array (count=$($matches.Count))" }
    return $matches[0].value
}

function Get-ParameterInt {
    param(
        [Parameter(Mandatory = $true)] $Parameters,
        [Parameter(Mandatory = $true)] [string] $Name
    )

    $raw = Get-ParameterValue -Parameters $Parameters -Name $Name
    $intValue = 0
    if (-not ([int]::TryParse([string]$raw, [ref]$intValue))) {
        throw "Parameter '$Name' must be a valid integer, got '$raw'"
    }
    return $intValue
}

function Get-CandidateIdFromPreconditions {
    param(
        [Parameter(Mandatory = $true)] $Preconditions
    )

    if ($null -eq $Preconditions) { throw "Preconditions array is null" }
    $matches = @($Preconditions | Where-Object { $_ -like "candidate_id:*" })
    if ($matches.Count -eq 0) { throw "No candidate_id precondition found in preconditions" }
    if ($matches.Count -gt 1) { throw "Multiple candidate_id preconditions found (count=$($matches.Count))" }
    $parts = $matches[0] -split ":", 2
    if ($parts.Count -lt 2 -or [string]::IsNullOrWhiteSpace($parts[1])) { throw "Malformed candidate_id precondition: $($matches[0])" }
    return $parts[1]
}

function Verify-SocialTalkLoopArtifacts {
    param(
        [Parameter(Mandatory = $true)] [string] $LoopRoot,
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [string] $RunDirectory,
        [Parameter(Mandatory = $true)] $BeforeSnapshot
    )

    $reportPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-training-loop-report.json"))
    $rankingPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\ranking-response-0001.json"))
    $dailyPlanPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\daily-plan-response-0001.json"))
    $queuePath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\compiled-queue-0001.json"))
    $executionPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\execution-0001.json"))
    $datasetPath = Join-Path $LoopRoot "datasets\live-training-feature-rows.jsonl"
    $episodePath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\plan-execution-episode-0001.json"))
    $beforeSnapshotPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\before-snapshot-0001.json"))
    $afterSnapshotPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\after-snapshot-0001.json"))

    $missing = @()
    foreach ($p in @($reportPath, $rankingPath, $dailyPlanPath, $queuePath, $executionPath, $datasetPath, $episodePath)) {
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { $missing += $p }
    }
    if ($missing.Count -gt 0) {
        throw "Missing talk loop artifacts: $($missing -join ', ')"
    }

    $ranking = Get-Content -LiteralPath $rankingPath -Raw | ConvertFrom-Json
    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json

    $rankedCandidates = @($ranking.ranked_event_candidates)
    $planSteps = @($dailyPlan.plan.steps)
    $socialPlanStepCount = 0
    foreach ($s in $planSteps) { if ($s.kind -eq "social_interact") { $socialPlanStepCount++ } }
    if ($socialPlanStepCount -ne 1) { throw "Expected exactly 1 social_interact plan step, found $socialPlanStepCount" }

    $moveIdx = -1
    $socialIdx = -1
    for ($i = 0; $i -lt $planSteps.Count; $i++) {
        if ($planSteps[$i].kind -eq "move_to_tile" -and $planSteps[$i].step_id -like "*move_to_social_stand*") { $moveIdx = $i }
        if ($planSteps[$i].kind -eq "social_interact") { $socialIdx = $i }
    }
    if ($moveIdx -lt 0) { throw "Daily plan missing move_to_tile step with move_to_social_stand in step_id" }
    if ($socialIdx -lt 0) { throw "Daily plan missing social_interact step" }
    if ($moveIdx -ge $socialIdx) { throw "Daily plan has social_interact step before move_to_social_stand" }

    $moveStep = $planSteps[$moveIdx]
    $socialStep = $planSteps[$socialIdx]
    if ($moveStep.target_location -ne $socialStep.target_location) { throw "Move and social plan step target_location mismatch" }
    if ($null -eq $moveStep.target_tile_x -or $null -eq $moveStep.target_tile_y) { throw "Move step missing target tile" }
    if ($null -eq $socialStep.target_tile_x -or $null -eq $socialStep.target_tile_y) { throw "Social step missing target tile" }
    if ($moveStep.target_tile_x -eq $socialStep.target_tile_x -and $moveStep.target_tile_y -eq $socialStep.target_tile_y) {
        throw "Move stand tile equals NPC tile - should be distinct"
    }

    $planCandidateId = Get-CandidateIdFromPreconditions -Preconditions $socialStep.preconditions
    $moveStepCandidateId = Get-CandidateIdFromPreconditions -Preconditions $moveStep.preconditions
    if ($moveStepCandidateId -ne $planCandidateId) { throw "Move step candidate_id '$moveStepCandidateId' does not match social step candidate_id '$planCandidateId'" }

    $matchingRanked = @($rankedCandidates | Where-Object {
        [string]$_.candidate_id -eq $planCandidateId -and
        $_.kind -eq "social_talk_current" -and
        $_.option_id -eq "social.talk_npc"
    })
    if ($matchingRanked.Count -ne 1) { throw "Expected exactly 1 ranked candidate matching plan candidate_id '$planCandidateId', found $($matchingRanked.Count)" }
    $selectedCandidate = $matchingRanked[0]
    if ($selectedCandidate.available -ne $true) { throw "Ranked talk candidate '$planCandidateId' is not available" }
    if ($selectedCandidate.timeline_status -eq "blocked") { throw "Ranked talk candidate '$planCandidateId' is blocked" }

    $socialStepParams = @{}
    foreach ($p in @($socialStep.parameters)) { $socialStepParams[$p.name] = $p.value }
    if ($socialStepParams['social_action_kind'] -ne "talk") { throw "Social plan step action_kind is not talk" }
    if (-not $socialStepParams['npc_name']) { throw "Social plan step missing npc_name" }
    if ($null -eq $socialStepParams['npc_tile_x'] -or $null -eq $socialStepParams['npc_tile_y']) { throw "Social plan step missing npc_tile" }

    $candidateNpc = [string](Get-ParameterValue -Parameters $selectedCandidate.parameters -Name "npc_name")
    $candidateLoc = [string]$selectedCandidate.location_id
    if ($socialStepParams['npc_name'] -ne $candidateNpc) { throw "Plan step npc_name '$($socialStepParams['npc_name'])' does not match candidate '$candidateNpc'" }

    $candidateNpcTileX = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "npc_tile_x"
    $candidateNpcTileY = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "npc_tile_y"
    $candidateStandTileX = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "stand_tile_x"
    $candidateStandTileY = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "stand_tile_y"

    if ($candidateLoc -ne $moveStep.target_location) { throw "Candidate location '$candidateLoc' does not match move plan target_location '$($moveStep.target_location)'" }
    if ($candidateLoc -ne $socialStep.target_location) { throw "Candidate location '$candidateLoc' does not match social plan target_location '$($socialStep.target_location)'" }

    if ([int]$moveStep.target_tile_x -ne $candidateStandTileX) { throw "Move target_tile_x '$($moveStep.target_tile_x)' does not match candidate stand_tile_x '$candidateStandTileX'" }
    if ([int]$moveStep.target_tile_y -ne $candidateStandTileY) { throw "Move target_tile_y '$($moveStep.target_tile_y)' does not match candidate stand_tile_y '$candidateStandTileY'" }

    if ([int]$socialStep.target_tile_x -ne $candidateNpcTileX) { throw "Social target_tile_x '$($socialStep.target_tile_x)' does not match candidate npc_tile_x '$candidateNpcTileX'" }
    if ([int]$socialStep.target_tile_y -ne $candidateNpcTileY) { throw "Social target_tile_y '$($socialStep.target_tile_y)' does not match candidate npc_tile_y '$candidateNpcTileY'" }

    if ($null -eq $selectedCandidate.tile_x) { throw "Ranked talk candidate missing top-level tile_x" }
    if ($null -eq $selectedCandidate.tile_y) { throw "Ranked talk candidate missing top-level tile_y" }
    $candidateTopTileX = [int]$selectedCandidate.tile_x
    $candidateTopTileY = [int]$selectedCandidate.tile_y
    if ($candidateTopTileX -ne $candidateStandTileX) { throw "Candidate top-level tile_x '$candidateTopTileX' does not match stand_tile_x '$candidateStandTileX'" }
    if ($candidateTopTileY -ne $candidateStandTileY) { throw "Candidate top-level tile_y '$candidateTopTileY' does not match stand_tile_y '$candidateStandTileY'" }

    $queueItems = @($queue.items)
    $socialQueueItemCount = 0
    foreach ($qi in $queueItems) { if ($qi.option_id -eq "executor.social_interact") { $socialQueueItemCount++ } }
    if ($socialQueueItemCount -ne 1) { throw "Expected exactly 1 executor.social_interact queue item, found $socialQueueItemCount" }

    $qMoveIdx = -1
    $qSocialIdx = -1
    for ($i = 0; $i -lt $queueItems.Count; $i++) {
        if ($queueItems[$i].option_id -eq "executor.move_to_tile" -and $queueItems[$i].source_action_id -eq $moveStep.step_id) { $qMoveIdx = $i }
        if ($queueItems[$i].option_id -eq "executor.social_interact") { $qSocialIdx = $i }
    }
    if ($qMoveIdx -lt 0) { throw "Compiled queue missing executor.move_to_tile item with matching source_action_id" }
    if ($qSocialIdx -lt 0) { throw "Compiled queue missing executor.social_interact item" }
    if ($qMoveIdx -ge $qSocialIdx) { throw "Compiled queue has social_interact before move_to_tile" }

    $qMoveItem = $queueItems[$qMoveIdx]
    $qSocialItem = $queueItems[$qSocialIdx]
    $qSocialSourceActionId = [string]$qSocialItem.source_action_id
    if ($qSocialSourceActionId -ne $socialStep.step_id) { throw "Queue social item source_action_id '$qSocialSourceActionId' does not match social plan step_id '$($socialStep.step_id)'" }
    $qSocialParams = @{}
    foreach ($p in @($qSocialItem.normalized_command.parameters)) { $qSocialParams[$p.name] = $p.value }
    if ($qSocialParams['npc_name'] -ne $candidateNpc) { throw "Queue social item npc_name mismatch with candidate" }
    if ($qSocialParams['social_action_kind'] -ne "talk") { throw "Queue social item action_kind is not talk" }
    if ($qSocialParams['target_location'] -ne $candidateLoc) { throw "Queue social item target_location mismatch with candidate" }
    if ($null -eq $qSocialParams['npc_tile_x']) { throw "Queue social item missing npc_tile_x" }
    if ($null -eq $qSocialParams['npc_tile_y']) { throw "Queue social item missing npc_tile_y" }
    if ($null -eq $qSocialParams['stand_tile_x']) { throw "Queue social item missing stand_tile_x" }
    if ($null -eq $qSocialParams['stand_tile_y']) { throw "Queue social item missing stand_tile_y" }
    if ([int]$qSocialParams['npc_tile_x'] -ne $candidateNpcTileX) { throw "Queue social item npc_tile_x mismatch with candidate" }
    if ([int]$qSocialParams['npc_tile_y'] -ne $candidateNpcTileY) { throw "Queue social item npc_tile_y mismatch with candidate" }
    if ([int]$qSocialParams['stand_tile_x'] -ne $candidateStandTileX) { throw "Queue social item stand_tile_x mismatch with candidate" }
    if ([int]$qSocialParams['stand_tile_y'] -ne $candidateStandTileY) { throw "Queue social item stand_tile_y mismatch with candidate" }

    $verifiedSocialResults = @($execution.step_results | Where-Object {
        $_.option_id -eq "executor.social_interact" -and
        $_.status -eq "applied" -and
        $_.primitive_verification_status -eq "verified" -and
        $_.social_native_handled -eq $true
    })
    if ($verifiedSocialResults.Count -ne 1) { throw "Expected exactly 1 verified applied social_interact result, found $($verifiedSocialResults.Count)" }

    $step = $verifiedSocialResults[0]
    if ([string]$step.queue_item_id -ne [string]$qSocialItem.queue_item_id) {
        throw "Execution result queue_item_id '$($step.queue_item_id)' does not match queue social item queue_item_id '$($qSocialItem.queue_item_id)'"
    }
    $hasVerifiedExecution = $true
    $talkEvidence = @{
        npc_name = [string]$step.social_npc_name
        npc_location_before = [string]$step.social_npc_location_before
        npc_location_after = [string]$step.social_npc_location_after
        npc_tile_x_before = $step.social_npc_tile_x_before
        npc_tile_y_before = $step.social_npc_tile_y_before
        player_tile_x_before = $step.social_player_tile_x_before
        player_tile_y_before = $step.social_player_tile_y_before
        player_facing_before = $step.social_player_facing_before
        dialog_open_before = $step.social_dialogue_open_before
        dialog_open_after = $step.social_dialogue_open_after
        menu_open_before = $step.social_menu_open_before
        menu_open_after = $step.social_menu_open_after
        menu_type_before = [string]$step.social_menu_type_before
        menu_type_after = [string]$step.social_menu_type_after
        dialogue_count_before = $step.social_current_dialogue_count_before
        dialogue_count_after = $step.social_current_dialogue_count_after
        dialogue_key_before = [string]$step.social_current_dialogue_key_before
        dialogue_key_after = [string]$step.social_current_dialogue_key_after
        dialogue_speaker_before = [string]$step.social_current_dialogue_speaker_name_before
        dialogue_speaker_after = [string]$step.social_current_dialogue_speaker_name_after
        native_handled = $step.social_native_handled
        action_kind = [string]$step.social_action_kind
        talked_to_before = $step.social_talked_to_today_before
        talked_to_after = $step.social_talked_to_today_after
    }
    if ($talkEvidence.npc_name -ne $candidateNpc) { throw "Execution NPC name '$($talkEvidence.npc_name)' mismatch with candidate '$candidateNpc'" }
    if ($talkEvidence.npc_location_before -ne $candidateLoc) { throw "Execution NPC location '$($talkEvidence.npc_location_before)' mismatch with candidate '$candidateLoc'" }
    if ($talkEvidence.action_kind -ne "talk") { throw "Execution action kind is not talk" }

    if ($null -eq $talkEvidence.npc_tile_x_before) { throw "Talk execution missing npc_tile_x_before" }
    if ($null -eq $talkEvidence.npc_tile_y_before) { throw "Talk execution missing npc_tile_y_before" }
    if ($null -eq $talkEvidence.player_tile_x_before) { throw "Talk execution missing player_tile_x_before" }
    if ($null -eq $talkEvidence.player_tile_y_before) { throw "Talk execution missing player_tile_y_before" }
    if ($null -eq $talkEvidence.player_facing_before) { throw "Talk execution missing player_facing_before" }

    if ([int]$talkEvidence.npc_tile_x_before -ne $candidateNpcTileX) { throw "Execution npc_tile_x_before '$($talkEvidence.npc_tile_x_before)' mismatch with candidate npc_tile_x '$candidateNpcTileX'" }
    if ([int]$talkEvidence.npc_tile_y_before -ne $candidateNpcTileY) { throw "Execution npc_tile_y_before '$($talkEvidence.npc_tile_y_before)' mismatch with candidate npc_tile_y '$candidateNpcTileY'" }
    if ([int]$talkEvidence.player_tile_x_before -ne $candidateStandTileX) { throw "Execution player_tile_x_before '$($talkEvidence.player_tile_x_before)' mismatch with candidate stand_tile_x '$candidateStandTileX'" }
    if ([int]$talkEvidence.player_tile_y_before -ne $candidateStandTileY) { throw "Execution player_tile_y_before '$($talkEvidence.player_tile_y_before)' mismatch with candidate stand_tile_y '$candidateStandTileY'" }

    $dx = [Math]::Abs([int]$talkEvidence.player_tile_x_before - [int]$talkEvidence.npc_tile_x_before)
    $dy = [Math]::Abs([int]$talkEvidence.player_tile_y_before - [int]$talkEvidence.npc_tile_y_before)
    if (-not (($dx -eq 1 -and $dy -eq 0) -or ($dx -eq 0 -and $dy -eq 1))) {
        throw "Player not Manhattan-adjacent to NPC (player=($($talkEvidence.player_tile_x_before),$($talkEvidence.player_tile_y_before)), npc=($($talkEvidence.npc_tile_x_before),$($talkEvidence.npc_tile_y_before)))"
    }

    $facing = [int]$talkEvidence.player_facing_before
    $playerX = [int]$talkEvidence.player_tile_x_before
    $playerY = [int]$talkEvidence.player_tile_y_before
    $npcX = [int]$talkEvidence.npc_tile_x_before
    $npcY = [int]$talkEvidence.npc_tile_y_before
    $expectedFacing = -1
    if ($npcY -lt $playerY) { $expectedFacing = 0 }
    elseif ($npcX -gt $playerX) { $expectedFacing = 1 }
    elseif ($npcY -gt $playerY) { $expectedFacing = 2 }
    elseif ($npcX -lt $playerX) { $expectedFacing = 3 }
    if ($expectedFacing -eq -1) { throw "Player is on same tile as NPC -- cannot determine facing direction" }
    if ($facing -ne $expectedFacing) { throw "Player facing $facing does not point toward NPC (expected $expectedFacing, NPC at ($npcX,$npcY), player at ($playerX,$playerY))" }

    if ($null -eq $talkEvidence.talked_to_before) { throw "Talk evidence missing talked_to_before" }
    if ($null -eq $talkEvidence.talked_to_after) { throw "Talk evidence missing talked_to_after" }
    if ($talkEvidence.talked_to_after -ne $true) { throw "Talked to after is not true" }
    if ($null -eq $talkEvidence.dialog_open_before -or $null -eq $talkEvidence.dialog_open_after) {
        throw "Talk evidence missing dialogue open before/after"
    }
    $hasPostTalkDialogueSignal = ($talkEvidence.dialog_open_after -eq $true) -or ($talkEvidence.menu_open_after -eq $true) -or
        ($null -ne $talkEvidence.dialogue_count_after -and [int]$talkEvidence.dialogue_count_after -gt 0) -or
        (-not [string]::IsNullOrWhiteSpace($talkEvidence.dialogue_key_after))
    if (-not $hasPostTalkDialogueSignal) { throw "No post-talk dialogue/menu signal detected (dialog_open_after=$($talkEvidence.dialog_open_after), menu_open_after=$($talkEvidence.menu_open_after), dialogue_count_after=$($talkEvidence.dialogue_count_after), dialogue_key_after=$($talkEvidence.dialogue_key_after))" }

    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $RunDirectory "talk-loop-report.json") -Force
    Copy-Item -LiteralPath $rankingPath -Destination (Join-Path $RunDirectory "talk-ranking-response.json") -Force
    Copy-Item -LiteralPath $dailyPlanPath -Destination (Join-Path $RunDirectory "talk-daily-plan-response.json") -Force
    Copy-Item -LiteralPath $queuePath -Destination (Join-Path $RunDirectory "talk-compiled-queue.json") -Force
    Copy-Item -LiteralPath $executionPath -Destination (Join-Path $RunDirectory "talk-execution.json") -Force
    Copy-Item -LiteralPath $episodePath -Destination (Join-Path $RunDirectory "talk-episode.json") -Force
    if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
        Copy-Item -LiteralPath $datasetPath -Destination (Join-Path $RunDirectory "talk-feature-rows.jsonl") -Force
    }
    if (Test-Path -LiteralPath $beforeSnapshotPath -PathType Leaf) {
        Copy-Item -LiteralPath $beforeSnapshotPath -Destination (Join-Path $RunDirectory "bridge-snapshot-before.json") -Force
    }
    if (Test-Path -LiteralPath $afterSnapshotPath -PathType Leaf) {
        Copy-Item -LiteralPath $afterSnapshotPath -Destination (Join-Path $RunDirectory "bridge-snapshot-after-talk.json") -Force
    }

    return [PSCustomObject]@{
        HasRankedCandidate = $true
        HasMoveBeforeSocial = ($moveIdx -lt $socialIdx)
        HasQueueMoveBeforeSocial = ($qMoveIdx -lt $qSocialIdx)
        HasVerifiedExecution = $hasVerifiedExecution
        TalkEvidence = $talkEvidence
        DailyPlanPath = $dailyPlanPath
        QueuePath = $queuePath
        ExecutionPath = $executionPath
    }
}

function Verify-SocialGiftLoopArtifacts {
    param(
        [Parameter(Mandatory = $true)] [string] $LoopRoot,
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [string] $RunDirectory
    )

    $reportPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-training-loop-report.json"))
    $rankingPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\ranking-response-0001.json"))
    $dailyPlanPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\daily-plan-response-0001.json"))
    $queuePath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\compiled-queue-0001.json"))
    $executionPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\execution-0001.json"))
    $datasetPath = Join-Path $LoopRoot "datasets\live-training-feature-rows.jsonl"
    $episodePath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\plan-execution-episode-0001.json"))
    $afterSnapshotPath = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots\after-snapshot-0001.json"))

    $missing = @()
    foreach ($p in @($reportPath, $rankingPath, $dailyPlanPath, $queuePath, $executionPath, $datasetPath, $episodePath)) {
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { $missing += $p }
    }
    if ($missing.Count -gt 0) {
        throw "Missing gift loop artifacts: $($missing -join ', ')"
    }

    $ranking = Get-Content -LiteralPath $rankingPath -Raw | ConvertFrom-Json
    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json

    $rankedCandidates = @($ranking.ranked_event_candidates)
    $planSteps = @($dailyPlan.plan.steps)
    $socialPlanStepCount = 0
    foreach ($s in $planSteps) { if ($s.kind -eq "social_interact") { $socialPlanStepCount++ } }
    if ($socialPlanStepCount -ne 1) { throw "Gift: expected exactly 1 social_interact plan step, found $socialPlanStepCount" }

    $moveIdx = -1
    $socialIdx = -1
    for ($i = 0; $i -lt $planSteps.Count; $i++) {
        if ($planSteps[$i].kind -eq "move_to_tile" -and $planSteps[$i].step_id -like "*move_to_social_stand*") { $moveIdx = $i }
        if ($planSteps[$i].kind -eq "social_interact") { $socialIdx = $i }
    }
    if ($moveIdx -lt 0) { throw "Gift daily plan missing move_to_tile step with move_to_social_stand in step_id" }
    if ($socialIdx -lt 0) { throw "Gift daily plan missing social_interact step" }
    if ($moveIdx -ge $socialIdx) { throw "Gift daily plan has social_interact before move_to_social_stand" }

    $moveStep = $planSteps[$moveIdx]
    $socialStep = $planSteps[$socialIdx]
    if ($moveStep.target_location -ne $socialStep.target_location) { throw "Gift move and social plan step target_location mismatch" }
    if ($null -eq $moveStep.target_tile_x -or $null -eq $moveStep.target_tile_y) { throw "Gift move step missing target tile" }
    if ($null -eq $socialStep.target_tile_x -or $null -eq $socialStep.target_tile_y) { throw "Gift social step missing target tile" }
    if ($moveStep.target_tile_x -eq $socialStep.target_tile_x -and $moveStep.target_tile_y -eq $socialStep.target_tile_y) {
        throw "Gift move stand tile equals NPC tile - should be distinct"
    }

    $planCandidateId = Get-CandidateIdFromPreconditions -Preconditions $socialStep.preconditions
    $moveStepCandidateId = Get-CandidateIdFromPreconditions -Preconditions $moveStep.preconditions
    if ($moveStepCandidateId -ne $planCandidateId) { throw "Gift move step candidate_id '$moveStepCandidateId' does not match social step candidate_id '$planCandidateId'" }

    $matchingRanked = @($rankedCandidates | Where-Object {
        [string]$_.candidate_id -eq $planCandidateId -and
        $_.kind -eq "social_gift_current" -and
        $_.option_id -eq "social.gift_npc"
    })
    if ($matchingRanked.Count -ne 1) { throw "Gift: expected exactly 1 ranked candidate matching plan candidate_id '$planCandidateId', found $($matchingRanked.Count)" }
    $selectedCandidate = $matchingRanked[0]
    if ($selectedCandidate.available -ne $true) { throw "Gift ranked candidate '$planCandidateId' is not available" }
    if ($selectedCandidate.timeline_status -eq "blocked") { throw "Gift ranked candidate '$planCandidateId' is blocked" }

    $socialStepParams = @{}
    foreach ($p in @($socialStep.parameters)) { $socialStepParams[$p.name] = $p.value }
    if ($socialStepParams['social_action_kind'] -ne "gift") { throw "Gift social plan step action_kind is not gift" }
    if (-not $socialStepParams['slot_index'] -and -not $socialStepParams['qualified_item_id']) { throw "Gift social plan step missing slot_index/qualified_item_id" }

    $candidateNpc = [string](Get-ParameterValue -Parameters $selectedCandidate.parameters -Name "npc_name")
    $candidateLoc = [string]$selectedCandidate.location_id
    if ($socialStepParams['npc_name'] -ne $candidateNpc) { throw "Gift plan step npc_name mismatch with candidate" }

    $candidateNpcTileX = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "npc_tile_x"
    $candidateNpcTileY = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "npc_tile_y"
    $candidateStandTileX = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "stand_tile_x"
    $candidateStandTileY = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "stand_tile_y"

    if ($candidateLoc -ne $moveStep.target_location) { throw "Gift candidate location '$candidateLoc' does not match move plan target_location '$($moveStep.target_location)'" }
    if ($candidateLoc -ne $socialStep.target_location) { throw "Gift candidate location '$candidateLoc' does not match social plan target_location '$($socialStep.target_location)'" }

    if ([int]$moveStep.target_tile_x -ne $candidateStandTileX) { throw "Gift move target_tile_x '$($moveStep.target_tile_x)' does not match candidate stand_tile_x '$candidateStandTileX'" }
    if ([int]$moveStep.target_tile_y -ne $candidateStandTileY) { throw "Gift move target_tile_y '$($moveStep.target_tile_y)' does not match candidate stand_tile_y '$candidateStandTileY'" }

    if ([int]$socialStep.target_tile_x -ne $candidateNpcTileX) { throw "Gift social target_tile_x '$($socialStep.target_tile_x)' does not match candidate npc_tile_x '$candidateNpcTileX'" }
    if ([int]$socialStep.target_tile_y -ne $candidateNpcTileY) { throw "Gift social target_tile_y '$($socialStep.target_tile_y)' does not match candidate npc_tile_y '$candidateNpcTileY'" }

    if ($null -eq $selectedCandidate.tile_x) { throw "Gift ranked candidate missing top-level tile_x" }
    if ($null -eq $selectedCandidate.tile_y) { throw "Gift ranked candidate missing top-level tile_y" }
    $candidateTopTileX = [int]$selectedCandidate.tile_x
    $candidateTopTileY = [int]$selectedCandidate.tile_y
    if ($candidateTopTileX -ne $candidateStandTileX) { throw "Gift candidate top-level tile_x '$candidateTopTileX' does not match stand_tile_x '$candidateStandTileX'" }
    if ($candidateTopTileY -ne $candidateStandTileY) { throw "Gift candidate top-level tile_y '$candidateTopTileY' does not match stand_tile_y '$candidateStandTileY'" }

    $candidateSlot = [int](Get-ParameterValue -Parameters $selectedCandidate.parameters -Name "slot_index")
    $candidateQualifiedItem = [string](Get-ParameterValue -Parameters $selectedCandidate.parameters -Name "qualified_item_id")
    $candidateItemStackBefore = Get-ParameterInt -Parameters $selectedCandidate.parameters -Name "item_stack_before"
    $candidateItemStackBeforeInt = [int]$candidateItemStackBefore
    $giftUpdatesNormalRaw = Get-ParameterValue -Parameters $selectedCandidate.parameters -Name "gift_updates_normal_limits"
    $candidateGiftUpdatesNormal = $false
    if ($null -ne $giftUpdatesNormalRaw) {
        $candidateGiftUpdatesNormal = [bool]::Parse([string]$giftUpdatesNormalRaw)
    }

    $queueItems = @($queue.items)
    $socialQueueItemCount = 0
    foreach ($qi in $queueItems) { if ($qi.option_id -eq "executor.social_interact") { $socialQueueItemCount++ } }
    if ($socialQueueItemCount -ne 1) { throw "Gift: expected exactly 1 executor.social_interact queue item, found $socialQueueItemCount" }

    $qMoveIdx = -1
    $qSocialIdx = -1
    for ($i = 0; $i -lt $queueItems.Count; $i++) {
        if ($queueItems[$i].option_id -eq "executor.move_to_tile" -and $queueItems[$i].source_action_id -eq $moveStep.step_id) { $qMoveIdx = $i }
        if ($queueItems[$i].option_id -eq "executor.social_interact") { $qSocialIdx = $i }
    }
    if ($qMoveIdx -lt 0) { throw "Gift compiled queue missing executor.move_to_tile with matching source_action_id" }
    if ($qSocialIdx -lt 0) { throw "Gift compiled queue missing executor.social_interact item" }
    if ($qMoveIdx -ge $qSocialIdx) { throw "Gift compiled queue has social_interact before move_to_tile" }

    $qSocialItem = $queueItems[$qSocialIdx]
    $qSocialSourceActionId = [string]$qSocialItem.source_action_id
    if ($qSocialSourceActionId -ne $socialStep.step_id) { throw "Gift queue social item source_action_id does not match social plan step_id" }
    $qSocialParams = @{}
    foreach ($p in @($qSocialItem.normalized_command.parameters)) { $qSocialParams[$p.name] = $p.value }
    if ($qSocialParams['npc_name'] -ne $candidateNpc) { throw "Gift queue npc_name mismatch with candidate" }
    if ($qSocialParams['social_action_kind'] -ne "gift") { throw "Gift queue action_kind is not gift" }
    if ($qSocialParams['target_location'] -ne $candidateLoc) { throw "Gift queue target_location mismatch with candidate" }
    if ($null -eq $qSocialParams['npc_tile_x']) { throw "Gift queue social item missing npc_tile_x" }
    if ($null -eq $qSocialParams['npc_tile_y']) { throw "Gift queue social item missing npc_tile_y" }
    if ($null -eq $qSocialParams['stand_tile_x']) { throw "Gift queue social item missing stand_tile_x" }
    if ($null -eq $qSocialParams['stand_tile_y']) { throw "Gift queue social item missing stand_tile_y" }
    if ($null -eq $qSocialParams['slot_index']) { throw "Gift queue social item missing slot_index" }
    if ($null -eq $qSocialParams['qualified_item_id']) { throw "Gift queue social item missing qualified_item_id" }
    if ($null -eq $qSocialParams['item_stack_before']) { throw "Gift queue social item missing item_stack_before" }
    if ([int]$qSocialParams['npc_tile_x'] -ne $candidateNpcTileX) { throw "Gift queue npc_tile_x mismatch with candidate" }
    if ([int]$qSocialParams['npc_tile_y'] -ne $candidateNpcTileY) { throw "Gift queue npc_tile_y mismatch with candidate" }
    if ([int]$qSocialParams['stand_tile_x'] -ne $candidateStandTileX) { throw "Gift queue stand_tile_x mismatch with candidate" }
    if ([int]$qSocialParams['stand_tile_y'] -ne $candidateStandTileY) { throw "Gift queue stand_tile_y mismatch with candidate" }
    if ([int]$qSocialParams['slot_index'] -ne $candidateSlot) { throw "Gift queue slot_index mismatch with candidate" }
    if ([string]$qSocialParams['qualified_item_id'] -ne $candidateQualifiedItem) { throw "Gift queue qualified_item_id mismatch with candidate" }
    if ([int]$qSocialParams['item_stack_before'] -ne $candidateItemStackBeforeInt) { throw "Gift queue item_stack_before mismatch with candidate" }

    $verifiedGiftResults = @($execution.step_results | Where-Object {
        $_.option_id -eq "executor.social_interact" -and
        $_.status -eq "applied" -and
        $_.primitive_verification_status -eq "verified" -and
        $_.social_native_handled -eq $true
    })
    if ($verifiedGiftResults.Count -ne 1) { throw "Gift: expected exactly 1 verified applied social_interact result, found $($verifiedGiftResults.Count)" }

    $step = $verifiedGiftResults[0]
    if ([string]$step.queue_item_id -ne [string]$qSocialItem.queue_item_id) {
        throw "Gift execution result queue_item_id '$($step.queue_item_id)' does not match queue social item queue_item_id '$($qSocialItem.queue_item_id)'"
    }
    $hasVerifiedExecution = $true
    $giftEvidence = @{
        npc_name = [string]$step.social_npc_name
        action_kind = [string]$step.social_action_kind
        native_handled = $step.social_native_handled
        gift_item_id_before = [string]$step.social_gift_item_id_before
        gift_item_id_after = [string]$step.social_gift_item_id_after
        gift_stack_before = $step.social_gift_stack_before
        gift_stack_after = $step.social_gift_stack_after
        gift_slot_before = $step.social_gift_slot_before
        gift_quality_before = $step.social_gift_quality_before
        gift_quality_after = $step.social_gift_quality_after
        npc_tile_x_before = $step.social_npc_tile_x_before
        npc_tile_y_before = $step.social_npc_tile_y_before
        npc_location_before = [string]$step.social_npc_location_before
        player_tile_x_before = $step.social_player_tile_x_before
        player_tile_y_before = $step.social_player_tile_y_before
        player_facing_before = $step.social_player_facing_before
        friendship_before = $step.social_friendship_points_before
        friendship_after = $step.social_friendship_points_after
        gifts_today_before = $step.social_gifts_today_before
        gifts_today_after = $step.social_gifts_today_after
        gifts_week_before = $step.social_gifts_this_week_before
        gifts_week_after = $step.social_gifts_this_week_after
    }
    if ($giftEvidence.action_kind -ne "gift") { throw "Gift execution action kind is not gift" }
    if ($giftEvidence.npc_name -ne $candidateNpc) { throw "Gift execution NPC name mismatch with candidate" }
    if ($giftEvidence.npc_location_before -ne $candidateLoc) { throw "Gift execution NPC location mismatch with candidate" }

    if ($null -eq $giftEvidence.npc_tile_x_before) { throw "Gift execution missing npc_tile_x_before" }
    if ($null -eq $giftEvidence.npc_tile_y_before) { throw "Gift execution missing npc_tile_y_before" }
    if ($null -eq $giftEvidence.player_tile_x_before) { throw "Gift execution missing player_tile_x_before" }
    if ($null -eq $giftEvidence.player_tile_y_before) { throw "Gift execution missing player_tile_y_before" }
    if ($null -eq $giftEvidence.player_facing_before) { throw "Gift execution missing player_facing_before" }

    if ([int]$giftEvidence.npc_tile_x_before -ne $candidateNpcTileX) { throw "Gift execution npc_tile_x_before mismatch with candidate" }
    if ([int]$giftEvidence.npc_tile_y_before -ne $candidateNpcTileY) { throw "Gift execution npc_tile_y_before mismatch with candidate" }
    if ([int]$giftEvidence.player_tile_x_before -ne $candidateStandTileX) { throw "Gift execution player_tile_x_before mismatch with candidate stand tile" }
    if ([int]$giftEvidence.player_tile_y_before -ne $candidateStandTileY) { throw "Gift execution player_tile_y_before mismatch with candidate stand tile" }

    $giftDx = [Math]::Abs([int]$giftEvidence.player_tile_x_before - [int]$giftEvidence.npc_tile_x_before)
    $giftDy = [Math]::Abs([int]$giftEvidence.player_tile_y_before - [int]$giftEvidence.npc_tile_y_before)
    if (-not (($giftDx -eq 1 -and $giftDy -eq 0) -or ($giftDx -eq 0 -and $giftDy -eq 1))) {
        throw "Gift player not Manhattan-adjacent to NPC (player=($($giftEvidence.player_tile_x_before),$($giftEvidence.player_tile_y_before)), npc=($($giftEvidence.npc_tile_x_before),$($giftEvidence.npc_tile_y_before)))"
    }

    $giftFacing = [int]$giftEvidence.player_facing_before
    $giftPlayerX = [int]$giftEvidence.player_tile_x_before
    $giftPlayerY = [int]$giftEvidence.player_tile_y_before
    $giftNpcX = [int]$giftEvidence.npc_tile_x_before
    $giftNpcY = [int]$giftEvidence.npc_tile_y_before
    $giftExpectedFacing = -1
    if ($giftNpcY -lt $giftPlayerY) { $giftExpectedFacing = 0 }
    elseif ($giftNpcX -gt $giftPlayerX) { $giftExpectedFacing = 1 }
    elseif ($giftNpcY -gt $giftPlayerY) { $giftExpectedFacing = 2 }
    elseif ($giftNpcX -lt $giftPlayerX) { $giftExpectedFacing = 3 }
    if ($giftExpectedFacing -eq -1) { throw "Gift player is on same tile as NPC -- cannot determine facing direction" }
    if ($giftFacing -ne $giftExpectedFacing) { throw "Gift player facing $giftFacing does not point toward NPC (expected $giftExpectedFacing, NPC at ($giftNpcX,$giftNpcY), player at ($giftPlayerX,$giftPlayerY))" }

    $execGiftSlot = $giftEvidence.gift_slot_before
    if ($null -eq $execGiftSlot) { throw "Gift execution missing gift_slot_before" }
    if ([int]$execGiftSlot -ne $candidateSlot) {
        throw "Gift slot mismatch: candidate=$candidateSlot, execution=$execGiftSlot"
    }
    $execGiftItemId = $giftEvidence.gift_item_id_before
    if ([string]::IsNullOrWhiteSpace($candidateQualifiedItem)) { throw "Gift candidate missing qualified_item_id" }
    if ([string]::IsNullOrWhiteSpace($execGiftItemId)) { throw "Gift execution missing gift_item_id_before" }
    if ($execGiftItemId -ne $candidateQualifiedItem) {
        throw "Gift qualified_item_id mismatch: candidate=$candidateQualifiedItem, execution=$execGiftItemId"
    }

    $stackBefore = $giftEvidence.gift_stack_before
    $stackAfter = $giftEvidence.gift_stack_after
    if ($null -eq $stackBefore) { throw "Gift execution missing gift_stack_before" }
    $stackBeforeInt = [int]$stackBefore
    if ($stackBeforeInt -lt 1) { throw "Gift stack before must be >= 1, got $stackBeforeInt" }
    if ($stackBeforeInt -ne $candidateItemStackBeforeInt) {
        throw "Gift stack before mismatch: candidate=$candidateItemStackBeforeInt, execution=$stackBeforeInt"
    }
    if ($stackBeforeInt -gt 1) {
        if ($null -eq $stackAfter) { throw "Gift stack after must be non-null when before=$stackBeforeInt (expected $($stackBeforeInt-1))" }
        $expectedAfter = $stackBeforeInt - 1
        if ([int]$stackAfter -ne $expectedAfter) {
            throw "Gift stack did not decrease by exactly 1 (before=$stackBeforeInt, after=$stackAfter)"
        }
    }
    elseif ($stackBeforeInt -eq 1) {
        if ($null -ne $stackAfter) {
            throw "Gift stack should be null when exactly one item consumed (after=$stackAfter)"
        }
    }

    if ($null -eq $giftEvidence.friendship_before) { throw "Gift execution missing friendship before" }
    if ($null -eq $giftEvidence.friendship_after) { throw "Gift execution missing friendship after" }

    if ($candidateGiftUpdatesNormal -eq $true) {
        if ($null -eq $giftEvidence.gifts_today_before) { throw "Gift execution missing gifts_today_before" }
        if ($null -eq $giftEvidence.gifts_today_after) { throw "Gift execution missing gifts_today_after" }
        if ([int]$giftEvidence.gifts_today_after -ne [int]$giftEvidence.gifts_today_before + 1) {
            throw "Gifts today did not increment by 1 (before=$($giftEvidence.gifts_today_before), after=$($giftEvidence.gifts_today_after))"
        }
        if ($null -eq $giftEvidence.gifts_week_before) { throw "Gift execution missing gifts_week_before" }
        if ($null -eq $giftEvidence.gifts_week_after) { throw "Gift execution missing gifts_week_after" }
        if ([int]$giftEvidence.gifts_week_after -ne [int]$giftEvidence.gifts_week_before + 1) {
            throw "Gifts this week did not increment by 1 (before=$($giftEvidence.gifts_week_before), after=$($giftEvidence.gifts_week_after))"
        }
    }

    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $RunDirectory "gift-loop-report.json") -Force
    Copy-Item -LiteralPath $rankingPath -Destination (Join-Path $RunDirectory "gift-ranking-response.json") -Force
    Copy-Item -LiteralPath $dailyPlanPath -Destination (Join-Path $RunDirectory "gift-daily-plan-response.json") -Force
    Copy-Item -LiteralPath $queuePath -Destination (Join-Path $RunDirectory "gift-compiled-queue.json") -Force
    Copy-Item -LiteralPath $executionPath -Destination (Join-Path $RunDirectory "gift-execution.json") -Force
    Copy-Item -LiteralPath $episodePath -Destination (Join-Path $RunDirectory "gift-episode.json") -Force
    if (Test-Path -LiteralPath $datasetPath -PathType Leaf) {
        Copy-Item -LiteralPath $datasetPath -Destination (Join-Path $RunDirectory "gift-feature-rows.jsonl") -Force
    }
    if (Test-Path -LiteralPath $afterSnapshotPath -PathType Leaf) {
        Copy-Item -LiteralPath $afterSnapshotPath -Destination (Join-Path $RunDirectory "bridge-snapshot-after-gift.json") -Force
    }

    return [PSCustomObject]@{
        HasRankedCandidate = $true
        HasMoveBeforeSocial = ($moveIdx -lt $socialIdx)
        HasQueueMoveBeforeSocial = ($qMoveIdx -lt $qSocialIdx)
        HasVerifiedExecution = $hasVerifiedExecution
        GiftEvidence = $giftEvidence
        DailyPlanPath = $dailyPlanPath
        QueuePath = $queuePath
        ExecutionPath = $executionPath
    }
}

function Get-PortConflict {
    param(
        [Parameter(Mandatory = $true)] [int] $Port
    )

    try {
        $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        if ($null -ne $connections -and $connections.Count -gt 0) {
            return @($connections | Select-Object LocalAddress, LocalPort, State, OwningProcess)
        }
    }
    catch {
    }

    return $null
}

function Test-PortConflictGuard {
    param(
        [Parameter(Mandatory = $true)] [int[]] $Ports
    )

    $conflicts = @()
    foreach ($port in $Ports) {
        $bindings = Get-PortConflict -Port $port
        if ($null -ne $bindings) {
            $conflicts += [PSCustomObject]@{
                Port = $port
                Status = "in_use"
                Bindings = $bindings
            }
        }
    }

    return $conflicts
}

function Verify-ProductionSocialRouteStepArtifacts {
    param(
        [Parameter(Mandatory = $true)] [string] $LoopRoot,
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [string] $RunDirectory,
        [Parameter(Mandatory = $true)] $BeforeSnapshot
    )

    $snapshotDir = Join-Path $LoopRoot (Join-Path "runs" (Join-Path $RunId "live-snapshots"))
    $rankingPath = Join-Path $snapshotDir "ranking-response-0001.json"
    $dailyPlanPath = Join-Path $snapshotDir "daily-plan-response-0001.json"
    $queuePath = Join-Path $snapshotDir "compiled-queue-0001.json"
    $executionPath = Join-Path $snapshotDir "execution-0001.json"
    $episodePath = Join-Path $snapshotDir "plan-execution-episode-0001.json"
    $afterSnapshotPath = Join-Path $snapshotDir "after-snapshot-0001.json"
    foreach ($path in @($rankingPath, $dailyPlanPath, $queuePath, $executionPath, $episodePath, $afterSnapshotPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Production social route step missing artifact: $path"
        }
    }

    $ranking = Get-Content -LiteralPath $rankingPath -Raw | ConvertFrom-Json
    $dailyPlan = Get-Content -LiteralPath $dailyPlanPath -Raw | ConvertFrom-Json
    $queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
    $execution = Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json
    $afterSnapshot = Get-Content -LiteralPath $afterSnapshotPath -Raw | ConvertFrom-Json

    $routeCandidates = @($ranking.ranked_event_candidates | Where-Object {
        $_.option_id -eq "social.talk_npc" -and
        $_.kind -eq "route_connector_tile" -and
        $_.available -eq $true -and
        $_.timeline_status -ne "blocked"
    })
    if ($routeCandidates.Count -lt 1) {
        throw "Production social route step ranking has no available route_connector_tile candidate"
    }

    $planSteps = @($dailyPlan.plan.steps)
    if ($planSteps.Count -ne 1 -or $planSteps[0].kind -ne "traverse_connector") {
        throw "Production social route step must compile exactly one traverse_connector plan step"
    }
    $planStep = $planSteps[0]
    $candidateId = Get-CandidateIdFromPreconditions -Preconditions $planStep.preconditions
    $selected = @($routeCandidates | Where-Object { [string]$_.candidate_id -eq $candidateId })
    if ($selected.Count -ne 1) {
        throw "Production social route plan candidate_id '$candidateId' does not bind exactly one ranked route candidate"
    }
    $candidate = $selected[0]

    $npcName = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "continuation.npc_name")
    $continuationOption = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "continuation.option_id")
    $finalTargetLocation = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "continuation.target_location")
    $nextLocation = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "expected_target_location")
    $positionSource = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "social_route.position_source")
    $futureProjection = [string](Get-ParameterValue -Parameters $candidate.parameters -Name "social_route.future_schedule_projection")
    $remainingConnectorCount = Get-ParameterInt -Parameters $candidate.parameters -Name "social_route.remaining_connector_count"
    if ($continuationOption -ne "social.talk_npc") { throw "Production social route continuation option mismatch: $continuationOption" }
    if ([string]::IsNullOrWhiteSpace($npcName)) { throw "Production social route continuation NPC is empty" }
    if ([string]::IsNullOrWhiteSpace($finalTargetLocation)) { throw "Production social route final target location is empty" }
    if ([string]::IsNullOrWhiteSpace($nextLocation)) { throw "Production social route next location is empty" }
    if ($positionSource -ne "npcs.social_interaction.current_loaded_instance") { throw "Production social route position source mismatch: $positionSource" }
    if ($futureProjection -ne "not_used") { throw "Production social route unexpectedly used future schedule projection: $futureProjection" }
    if ($remainingConnectorCount -lt 1) { throw "Production social route remaining connector count must be positive" }

    $planParams = @($planStep.parameters)
    if ((Get-ParameterValue -Parameters $planParams -Name "continuation.npc_name") -ne $npcName) { throw "Plan lost social continuation NPC" }
    if ((Get-ParameterValue -Parameters $planParams -Name "continuation.target_location") -ne $finalTargetLocation) { throw "Plan lost social continuation target" }
    if ((Get-ParameterValue -Parameters $planParams -Name "expected_target_location") -ne $nextLocation) { throw "Plan connector target mismatch" }
    if (-not (@($planStep.expected_effects) -contains "fresh_snapshot_replan_required=true")) { throw "Plan does not require fresh snapshot replan" }

    $queueItems = @($queue.items)
    if ($queueItems.Count -ne 1 -or $queueItems[0].option_id -ne "executor.traverse_connector") {
        throw "Production social route plan must compile exactly one executor.traverse_connector queue item"
    }
    $queueItem = $queueItems[0]
    $queueParams = @($queueItem.normalized_command.parameters)
    if ((Get-ParameterValue -Parameters $queueParams -Name "continuation.npc_name") -ne $npcName) { throw "Queue lost social continuation NPC" }
    if ((Get-ParameterValue -Parameters $queueParams -Name "continuation.target_location") -ne $finalTargetLocation) { throw "Queue lost social continuation target" }
    if ((Get-ParameterValue -Parameters $queueParams -Name "expected_target_location") -ne $nextLocation) { throw "Queue connector target mismatch" }

    $executionResults = if ($execution.PSObject.Properties.Name -contains "steps") {
        @($execution.steps)
    }
    else {
        @($execution)
    }
    $verified = @($executionResults | Where-Object {
        $_.option_id -eq "executor.traverse_connector" -and
        $_.status -eq "applied" -and
        $_.primitive_verification_status -eq "verified"
    })
    if ($verified.Count -ne 1) {
        throw "Production social route execution must contain one applied/verified traverse_connector result"
    }

    $beforeLocation = Get-SnapshotString $BeforeSnapshot "player" "location_id"
    $afterLocation = Get-SnapshotString $afterSnapshot "player" "location_id"
    if ($afterLocation -ne $nextLocation) {
        throw "Production social route arrival mismatch: expected '$nextLocation', got '$afterLocation'"
    }
    if ($afterSnapshot.state_hash -eq $BeforeSnapshot.state_hash) {
        throw "Production social route after snapshot did not change state hash"
    }

    Copy-Item -LiteralPath $rankingPath -Destination (Join-Path $RunDirectory "production-route-ranking-response.json") -Force
    Copy-Item -LiteralPath $dailyPlanPath -Destination (Join-Path $RunDirectory "production-route-daily-plan-response.json") -Force
    Copy-Item -LiteralPath $queuePath -Destination (Join-Path $RunDirectory "production-route-compiled-queue.json") -Force
    Copy-Item -LiteralPath $executionPath -Destination (Join-Path $RunDirectory "production-route-execution.json") -Force
    Copy-Item -LiteralPath $episodePath -Destination (Join-Path $RunDirectory "production-route-episode.json") -Force
    Copy-Item -LiteralPath $afterSnapshotPath -Destination (Join-Path $RunDirectory "production-route-after-snapshot.json") -Force

    return [PSCustomObject]@{
        Verified = $true
        NpcName = $npcName
        BeforeLocation = $beforeLocation
        NextLocation = $nextLocation
        FinalTargetLocation = $finalTargetLocation
        RemainingConnectorCount = $remainingConnectorCount
        AfterSnapshot = $afterSnapshot
    }
}

function Verify-ProductionSocialPursuitArtifacts {
    param(
        [Parameter(Mandatory = $true)] [string] $LoopRoot,
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [string] $RunDirectory,
        [string] $ExpectedContinuationOption = "social.talk_npc",
        [switch] $RequireSingleItemGiftConsumed
    )

    $runRoot = Join-Path $LoopRoot (Join-Path "runs" $RunId)
    $snapshotDir = Join-Path $runRoot "live-snapshots"
    $reportPath = Join-Path $runRoot "live-training-loop-report.json"
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "Production social pursuit report missing: $reportPath"
    }

    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.social_objective_completed -ne $true) {
        throw "Production social pursuit did not complete the locked social objective"
    }
    if ($null -ne $report.active_social_continuation) {
        throw "Production social pursuit completed with a stale continuation"
    }

    $rankingFiles = @(Get-ChildItem -LiteralPath $snapshotDir -Filter "ranking-response-*.json" -File | Where-Object { $_.BaseName -match '^ranking-response-[0-9]{4}$' } | Sort-Object Name)
    $executionFiles = @(Get-ChildItem -LiteralPath $snapshotDir -Filter "execution-*.json" -File | Where-Object { $_.BaseName -match '^execution-[0-9]{4}$' } | Sort-Object Name)
    $afterFiles = @(Get-ChildItem -LiteralPath $snapshotDir -Filter "after-snapshot-*.json" -File | Where-Object { $_.BaseName -match '^after-snapshot-[0-9]{4}$' } | Sort-Object Name)
    if ($rankingFiles.Count -lt 1 -or $executionFiles.Count -lt 1 -or $afterFiles.Count -lt 1) {
        throw "Production social pursuit artifacts are incomplete"
    }

    $lockedNpc = $null
    $routeApplied = 0
    $waitApplied = 0
    $socialApplied = 0
    $verifiedSocialResult = $null
    foreach ($rankingFile in $rankingFiles) {
        $ranking = Get-Content -LiteralPath $rankingFile.FullName -Raw | ConvertFrom-Json
        if ($ranking.social_continuation_filter.active -eq $true) {
            $objectiveNpc = [string]$ranking.social_continuation_filter.objective.npc_name
            $objectiveOption = [string]$ranking.social_continuation_filter.objective.option_id
            if ([string]::IsNullOrWhiteSpace($lockedNpc)) { $lockedNpc = $objectiveNpc }
            if ($objectiveNpc -ne $lockedNpc) { throw "Social pursuit switched NPC from '$lockedNpc' to '$objectiveNpc'" }
            if ($objectiveOption -ne $ExpectedContinuationOption) {
                throw "Social pursuit switched option from '$ExpectedContinuationOption' to '$objectiveOption'"
            }
            if ([int]$ranking.social_continuation_filter.selected_candidate_count -ne 1) {
                throw "Social continuation filter did not select exactly one candidate in $($rankingFile.Name)"
            }
        }
    }

    foreach ($executionFile in $executionFiles) {
        $execution = Get-Content -LiteralPath $executionFile.FullName -Raw | ConvertFrom-Json
        foreach ($step in @($execution.step_results)) {
            if ($step.status -ne "applied" -or $step.primitive_verification_status -ne "verified") { continue }
            $optionId = [string]$step.option_id
            if ($optionId -eq "executor.traverse_connector") { $routeApplied++ }
            elseif ($optionId -eq "executor.wait_ticks") { $waitApplied++ }
            elseif ($optionId -eq "executor.social_interact") {
                $socialApplied++
                $verifiedSocialResult = $step
            }
        }
    }
    if ($routeApplied -lt 1) { throw "Production social pursuit did not verify any connector traversal" }
    if ($socialApplied -ne 1) { throw "Production social pursuit expected one verified social interaction, found $socialApplied" }

    if ($RequireSingleItemGiftConsumed) {
        if ($null -eq $verifiedSocialResult) {
            throw "Production gift pursuit is missing its verified social result"
        }
        if ($verifiedSocialResult.social_action_kind -ne "gift") {
            throw "Production gift pursuit ended with action '$($verifiedSocialResult.social_action_kind)' instead of gift"
        }
        if ($verifiedSocialResult.social_gift_stack_before -ne 1) {
            throw "Production gift pursuit expected stack_before=1, got $($verifiedSocialResult.social_gift_stack_before)"
        }
        if ($null -ne $verifiedSocialResult.social_gift_stack_after) {
            throw "Production gift pursuit expected stack_after=null, got $($verifiedSocialResult.social_gift_stack_after)"
        }
        if ([string]::IsNullOrWhiteSpace([string]$verifiedSocialResult.social_gift_item_id_before)) {
            throw "Production gift pursuit did not record the consumed item identity"
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$verifiedSocialResult.social_gift_item_id_after)) {
            throw "Production gift pursuit retained an item identity after consuming the only item"
        }
    }

    $finalSnapshot = Get-Content -LiteralPath $afterFiles[-1].FullName -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $RunDirectory "production-pursuit-report.json") -Force
    Copy-Item -LiteralPath $afterFiles[-1].FullName -Destination (Join-Path $RunDirectory "production-pursuit-final-snapshot.json") -Force
    return [PSCustomObject]@{
        Verified = $true
        NpcName = $lockedNpc
        RouteStepsApplied = $routeApplied
        WaitStepsApplied = $waitApplied
        SocialInteractionsApplied = $socialApplied
        SocialActionKind = if ($null -eq $verifiedSocialResult) { "" } else { [string]$verifiedSocialResult.social_action_kind }
        GiftItemIdBefore = if ($null -eq $verifiedSocialResult) { "" } else { [string]$verifiedSocialResult.social_gift_item_id_before }
        GiftStackBefore = if ($null -eq $verifiedSocialResult) { $null } else { $verifiedSocialResult.social_gift_stack_before }
        GiftStackAfter = if ($null -eq $verifiedSocialResult) { $null } else { $verifiedSocialResult.social_gift_stack_after }
        Iterations = [int]$report.attempts_started
        FinalLocation = Get-SnapshotString $finalSnapshot "player" "location_id"
        FinalSnapshot = $finalSnapshot
    }
}

function Build-RouteEdgeTraverseRequest {
    param(
        [Parameter(Mandatory = $true)] $EdgeData,
        [Parameter(Mandatory = $true)] [string] $StateHash,
        [Parameter(Mandatory = $true)] [string] $SavesPath,
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [int] $EdgeIndex
    )

    $targetTileX = [int]$EdgeData.from_x
    $targetTileY = [int]$EdgeData.from_y
    $connectorKind = [string]$EdgeData.kind
    $expectedTargetLocation = [string]$EdgeData.target_location

    $traverseRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = "$RunId"
        queue_id = "runtime-native-social-smoke.route-bfs.$($EdgeData.from_location)"
        queue_item_id = "runtime-native-social-smoke.route-bfs.$($EdgeData.from_location).item.$EdgeIndex"
        before_state_hash = $StateHash
        option_id = "executor.traverse_connector"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $targetTileX
        target_tile_y = $targetTileY
        connector_kind = $connectorKind
        expected_target_location = $expectedTargetLocation
    }
    if ($null -ne $EdgeData.target_x) {
        $traverseRequest.expected_arrival_tile_x = [int]$EdgeData.target_x
    }
    if ($null -ne $EdgeData.target_y) {
        $traverseRequest.expected_arrival_tile_y = [int]$EdgeData.target_y
    }

    return $traverseRequest
}

function Invoke-RouteGraphBfsToNpc {
    param(
        [Parameter(Mandatory = $true)] $Snapshot,
        [Parameter(Mandatory = $true)] [string] $NpcName,
        [Parameter(Mandatory = $true)] [string] $ExecutorUrl,
        [Parameter(Mandatory = $true)] [string] $SavesPath,
        [Parameter(Mandatory = $true)] [string] $SnapshotUrl,
        [Parameter(Mandatory = $true)] [string] $RunDirectory,
        [Parameter(Mandatory = $true)] [string] $RunId
    )

    $playerLoc = Read-FieldValue $Snapshot "player" "location_id"
    if ([string]::IsNullOrWhiteSpace($playerLoc)) { throw "Cannot traverse route graph: missing player location_id" }
    $playerLoc = [string]$playerLoc

    $npcLocation = $null
    $socialInteraction = $Snapshot.state.npcs.social_interaction
    if ($null -ne $socialInteraction) {
        $rawValue = $socialInteraction.value
        if ($rawValue -is [array]) {
            foreach ($npc in $rawValue) {
                if ($npc.name -eq $NpcName) {
                    $npcLocation = [string]$npc.location_id
                    break
                }
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($npcLocation)) { throw "Cannot determine NPC location for $NpcName" }
    if ($npcLocation -eq $playerLoc) { return @{ arrived = $true; edges_traversed = 0 } }

    $routeGraphRaw = $Snapshot.state.locations.route_graph
    if ($null -eq $routeGraphRaw) { throw "No locations.route_graph in snapshot" }
    $graphValue = $routeGraphRaw.value
    if ($null -eq $graphValue) { throw "locations.route_graph value is null" }

    $rawEdges = @()
    if ($graphValue -is [array]) { $rawEdges = $graphValue }
    elseif ($graphValue.edges -is [array]) { $rawEdges = $graphValue.edges }
    if ($rawEdges.Count -eq 0) { throw "No route_graph edges available" }

    $edges = @($rawEdges | Where-Object {
        $_.resolved -eq $true -and
        -not [string]::IsNullOrWhiteSpace([string]$_.from_location) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.target_location) -and
        $null -ne $_.from_x -and $null -ne $_.from_y
    })
    if ($edges.Count -eq 0) { throw "No resolved route_graph edges with complete from_location, target_location, from_x, from_y" }

    foreach ($edge in $edges) {
        if ([string]::IsNullOrWhiteSpace([string]$edge.kind)) {
            throw "Route_graph edge missing or empty kind for $($edge.from_location) -> $($edge.target_location)"
        }
    }

    $adjacency = @{}
    foreach ($edge in $edges) {
        $src = [string]$edge.from_location
        $dst = [string]$edge.target_location
        if (-not $adjacency.ContainsKey($src)) { $adjacency[$src] = @{} }
        if (-not $adjacency[$src].ContainsKey($dst)) {
            $adjacency[$src][$dst] = $edge
        }
    }

    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($playerLoc)
    $visited = @{ $playerLoc = $true }
    $previousLocation = @{}
    $previousEdge = @{}
    $found = $false
    while ($queue.Count -gt 0) {
        $current = [string]$queue.Dequeue()
        if ($current -eq $npcLocation) { $found = $true; break }
        if (-not $adjacency.ContainsKey($current)) { continue }
        foreach ($next in $adjacency[$current].Keys) {
            if (-not $visited.ContainsKey($next)) {
                $visited[$next] = $true
                $previousLocation[$next] = $current
                $previousEdge[$next] = $adjacency[$current][$next]
                $queue.Enqueue($next)
            }
        }
    }

    if (-not $found) {
        $bfsArtifact = [ordered]@{
            status = "failed_closed"
            reason = "No bounded transparent route from $playerLoc to $npcLocation"
            player_location = $playerLoc
            npc_location = $npcLocation
            edges_count = $edges.Count
            reachable_locations = @($visited.Keys)
        }
        Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-failed.json") $bfsArtifact
        throw "No bounded transparent route exists from $playerLoc to $npcLocation"
    }

    $path = New-Object System.Collections.ArrayList
    $cursor = $npcLocation
    while ($cursor -ne $playerLoc) {
        $prev = $previousLocation[$cursor]
        $edgeItem = $previousEdge[$cursor]
        $path.Insert(0, [PSCustomObject]@{ from = $prev; to = $cursor; edge = $edgeItem })
        $cursor = $prev
    }

    $currentSnapshot = $Snapshot
    $edgeResults = @()
    foreach ($pathItem in $path) {
        $edgeData = $pathItem.edge
        if ([string]::IsNullOrWhiteSpace([string]$edgeData.kind)) {
            throw "Edge from $($edgeData.from_location) to $($edgeData.target_location) has empty kind"
        }

        $traverseRequest = Build-RouteEdgeTraverseRequest -EdgeData $edgeData -StateHash $currentSnapshot.state_hash -SavesPath $SavesPath -RunId $RunId -EdgeIndex $edgeResults.Count

        Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-edge-$($edgeResults.Count)-request.json") $traverseRequest
        try {
            $result = Invoke-JsonPost -Url "$ExecutorUrl/api/v1/training/execute" -Body $traverseRequest -TimeoutSeconds 60
            Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-edge-$($edgeResults.Count)-result.json") $result
        }
        catch {
            $failedArtifact = [ordered]@{
                status = "failed_closed"
                reason = "Traverse connector execution failed at edge $($edgeResults.Count): $($edgeData.from_location) -> $($edgeData.target_location)"
                from_location = [string]$edgeData.from_location
                target_location = [string]$edgeData.target_location
                error = $_.Exception.Message
            }
            Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-edge-$($edgeResults.Count)-failed.json") $failedArtifact
            throw "Traverse connector failed at edge from $($edgeData.from_location) to $($edgeData.target_location): $($_.Exception.Message)"
        }

        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            $unverifiedArtifact = [ordered]@{
                status = "failed_closed"
                reason = "Traverse connector result not applied/verified at edge $($edgeResults.Count): $($edgeData.from_location) -> $($edgeData.target_location)"
                result_status = $result.status
                primitive_verification_status = $result.primitive_verification_status
                block_reasons = @($result.block_reasons)
            }
            Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-edge-$($edgeResults.Count)-unverified.json") $unverifiedArtifact
            throw "Traverse connector result not applied/verified for $($edgeData.from_location) -> $($edgeData.target_location): status=$($result.status), verification=$($result.primitive_verification_status)"
        }

        Start-Sleep -Milliseconds 500

        $currentSnapshot = Wait-JsonHealth -Url $SnapshotUrl -TimeoutSeconds 30
        $currentLoc = Get-SnapshotString $currentSnapshot "player" "location_id"
        $expectedTargetLocation = $traverseRequest.expected_target_location
        if ($currentLoc -ne $expectedTargetLocation) {
            $badArrival = [ordered]@{
                status = "failed_closed"
                reason = "After traverse, player is in '$currentLoc' but expected '$expectedTargetLocation'"
                from_location = [string]$edgeData.from_location
                expected_target_location = $expectedTargetLocation
                actual_location = $currentLoc
            }
            Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-bad-arrival.json") $badArrival
            throw "Traverse arrival mismatch: expected $expectedTargetLocation, got $currentLoc"
        }

        if ($null -ne $edgeData.target_x -and $null -ne $edgeData.target_y) {
            $arrivalTileX = Get-SnapshotInt $currentSnapshot "player" "tile_x"
            $arrivalTileY = Get-SnapshotInt $currentSnapshot "player" "tile_y"
            if ($arrivalTileX -ne [int]$edgeData.target_x -or $arrivalTileY -ne [int]$edgeData.target_y) {
                $badTile = [ordered]@{
                    status = "failed_closed"
                    reason = "After traverse, player is at tile ($arrivalTileX, $arrivalTileY) but expected ($($edgeData.target_x), $($edgeData.target_y))"
                    expected_target_location = $expectedTargetLocation
                }
                Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-bad-arrival-tile.json") $badTile
                throw "Traverse arrival tile mismatch: expected ($($edgeData.target_x), $($edgeData.target_y)), got ($arrivalTileX, $arrivalTileY)"
            }
        }

        $edgeResults += [PSCustomObject]@{
            from_location = [string]$edgeData.from_location
            target_location = [string]$edgeData.target_location
            connector_kind = $traverseRequest.connector_kind
            target_tile_x = $traverseRequest.target_tile_x
            target_tile_y = $traverseRequest.target_tile_y
            result_status = $result.status
            primitive_verification_status = $result.primitive_verification_status
        }
    }

    $bfsSuccess = [ordered]@{
        status = "arrived"
        edges_traversed = $edgeResults.Count
        path = @($edgeResults)
    }
    Write-JsonFile (Join-Path $RunDirectory "route-graph-bfs-success.json") $bfsSuccess
    return [PSCustomObject]@{
        arrived = $true
        edges_traversed = $edgeResults.Count
        path = $edgeResults
    }
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"

if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}

if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}

if (-not (Test-Path -LiteralPath $RuntimeRoot -PathType Container)) {
    throw "Runtime root not found: $RuntimeRoot"
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$slotPath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $slotPath -PathType Container)) {
    throw "Isolated save slot not found: $slotPath"
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$routeCandidateLoopRoot = Join-Path $runDirectory "production-route-loop"
$talkLoopRoot = Join-Path $runDirectory "talk-loop"
$giftLoopRoot = Join-Path $runDirectory "gift-loop"
$trainingOutputDirectory = Join-Path $runDirectory "training-output"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null

$bridgePort = 8765
$executorPort = 8767

$portConflicts = Test-PortConflictGuard -Ports @($bridgePort, $executorPort, $BackendPort)
if ($portConflicts.Count -gt 0) {
    $conflictSummary = [ordered]@{
        status = "blocked"
        run_id = $RunId
        reason = "port_conflict_detected"
        conflicts = $portConflicts
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    }
    Write-JsonFile (Join-Path $runDirectory "port-conflict-guard.json") $conflictSummary
    $conflictSummary | ConvertTo-Json -Depth 32
    throw "Port conflict detected on required ports. Run 'Stop-RuntimeSmokeProcess' to clean up."
}

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$previousEnv = @{
    STARDEWAI_TEST_SAVES = $env:STARDEWAI_TEST_SAVES
    STARDEWAI_TEST_SLOT = $env:STARDEWAI_TEST_SLOT
    STARDEWAI_SAVE_ISOLATION_PATH = $env:STARDEWAI_SAVE_ISOLATION_PATH
    STARDEWAI_TRAINING_RUN_ID = $env:STARDEWAI_TRAINING_RUN_ID
    STARDEWAI_TRAINING_MODE = $env:STARDEWAI_TRAINING_MODE
    STARDEWAI_TRAINING_OUTPUT_DIR = $env:STARDEWAI_TRAINING_OUTPUT_DIR
    SDL_AUDIODRIVER = $env:SDL_AUDIODRIVER
    ALSOFT_DRIVERS = $env:ALSOFT_DRIVERS
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}

$gameProcess = $null
$backendProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:ASPNETCORE_URLS = $backendUrl

    $restoreLogPath = Join-Path $runDirectory "dotnet-restore-backend.log"
    dotnet restore (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj") 2>&1 | Set-Content -LiteralPath $restoreLogPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE for Backend. See $restoreLogPath" }

    $ltRestoreLogPath = Join-Path $runDirectory "dotnet-restore-livetrainingloop.log"
    dotnet restore (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") 2>&1 | Set-Content -LiteralPath $ltRestoreLogPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE for LiveTrainingLoop. See $ltRestoreLogPath" }

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    Wait-JsonHealth -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $executorHealth = Wait-JsonHealth -Url "$executorUrl/health" -TimeoutSeconds 30
    Start-Sleep -Seconds 20
    $worldSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    if ($ProductionGiftPursuitOnly) {
        $fixtureRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "$RunId.fixture"
            queue_item_id = "$RunId.fixture.single-gift"
            before_state_hash = $worldSnapshot.state_hash
            option_id = "debug.setup_single_gift_item"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            slot_index = 11
            qualified_item_id = "(O)388"
        }
        $fixtureResult = Invoke-JsonPost -Url "$executorUrl/api/v1/training/execute" -Body $fixtureRequest -TimeoutSeconds 60
        Write-JsonFile (Join-Path $runDirectory "single-gift-fixture-request.json") $fixtureRequest
        Write-JsonFile (Join-Path $runDirectory "single-gift-fixture-result.json") $fixtureResult
        if ($fixtureResult.status -ne "applied" -or $fixtureResult.primitive_verification_status -ne "verified") {
            throw "Single-gift isolated fixture was not applied/verified: $($fixtureResult.block_reasons -join ',')"
        }
        $worldSnapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    }
    $rawBeforeSnapshot = Invoke-RawJsonGet -Url $snapshotUrl
    $before = $rawBeforeSnapshot | ConvertFrom-Json
    Set-Content -LiteralPath (Join-Path $runDirectory "raw-snapshot-before.json") -Value $rawBeforeSnapshot -Encoding utf8
    $locationReadable = $false
    if ($null -ne $before.state -and $before.state.PSObject.Properties.Name -contains "player" -and $before.state.player.PSObject.Properties.Name -contains "location_id") {
        $locationReadable = $before.state.player.location_id.status -in @("available", "derived")
    }
    if (-not $locationReadable) { throw "Snapshot after wait does not have readable location_id" }
    $location = Get-SnapshotString $before "player" "location_id"
    $initialLocation = $location
    $productionRouteEvidence = $null

    $spouseField = $before.state.player.spouse
    if ($null -eq $spouseField -or $spouseField.status -notin @("available", "derived")) {
        throw "Snapshot after wait does not have readable player.spouse"
    }
    if ($null -eq $spouseField.value) {
        throw "Snapshot after wait encodes known no-spouse state as null instead of canonical empty string"
    }

    $ingestResult = Invoke-RawJsonPostStrict -Url "$backendUrl/api/v1/snapshots" -Json $rawBeforeSnapshot -ErrorArtifactPath (Join-Path $runDirectory "snapshot-ingest-error.json")
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-ingested.json") $ingestResult

    if ($ProductionGiftPursuitOnly) {
        dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
            --root $routeCandidateLoopRoot `
            --backend-url $backendUrl `
            --bridge-snapshot-url $snapshotUrl `
            --executor-url $executorUrl `
            --no-manifest `
            --run-id $RunId `
            --save-isolation-path $savesPath `
            --max-attempts 64 `
            --sleep-ms 0 `
            --skip-training `
            --use-daily-plan `
            --daily-plan-max-candidates 1 `
            --daily-plan-candidate-options "social.gift_npc" `
            --after-snapshot-wait-ms 1000 `
            --continue-after-blocked-queue-items `
            --stop-after-social-objective-complete
        if ($LASTEXITCODE -ne 0) { throw "Production gift pursuit LiveTrainingLoop returned exit code $LASTEXITCODE" }

        $giftPursuitEvidence = Verify-ProductionSocialPursuitArtifacts `
            -LoopRoot $routeCandidateLoopRoot `
            -RunId $RunId `
            -RunDirectory $runDirectory `
            -ExpectedContinuationOption "social.gift_npc" `
            -RequireSingleItemGiftConsumed
        Write-JsonFile (Join-Path $runDirectory "production-gift-pursuit-verification.json") $giftPursuitEvidence
        $giftPursuitSummary = [ordered]@{
            status = "passed"
            run_id = $RunId
            save_slot = $SaveSlot
            production_gift_pursuit_verified = $giftPursuitEvidence.Verified
            npc_name = $giftPursuitEvidence.NpcName
            connector_steps_applied = $giftPursuitEvidence.RouteStepsApplied
            wait_steps_applied = $giftPursuitEvidence.WaitStepsApplied
            social_interactions_applied = $giftPursuitEvidence.SocialInteractionsApplied
            social_action_kind = $giftPursuitEvidence.SocialActionKind
            gift_item_id_before = $giftPursuitEvidence.GiftItemIdBefore
            gift_stack_before = $giftPursuitEvidence.GiftStackBefore
            gift_stack_after = $giftPursuitEvidence.GiftStackAfter
            iterations = $giftPursuitEvidence.Iterations
            final_location = $giftPursuitEvidence.FinalLocation
            future_schedule_projection = "not_used"
            scope = "same_objective_multi_connector_single_item_gift_pursuit"
            artifacts_dir = $runDirectory
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $giftPursuitSummary
        $giftPursuitSummary | ConvertTo-Json -Depth 32
        return
    }

    $rankProbeRequest = [ordered]@{
        dataset_path = "$(Join-Path $talkLoopRoot "datasets\live-training-feature-rows.jsonl")"
        state_hash = $before.state_hash
        candidate_option_ids = @("social.talk_npc")
        include_blocked_options = $false
    }
    $rankProbe = Invoke-JsonPost -Url "$backendUrl/api/v1/planner/baseline/rank-options" -Body $rankProbeRequest -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "pre-talk-rank-probe.json") $rankProbe

    $hasSameLocationCandidate = $false
    $probeCandidates = @($rankProbe.ranked_event_candidates)
    foreach ($c in $probeCandidates) {
        if ($c.available -eq $true -and $c.kind -eq "social_talk_current" -and $c.timeline_status -ne "blocked" -and $c.option_id -eq "social.talk_npc") {
            $candidateLoc = $c.location_id
            if (-not [string]::IsNullOrWhiteSpace($candidateLoc) -and $candidateLoc -eq $location) {
                $hasSameLocationCandidate = $true
                break
            }
        }
    }
    if ($ProductionRouteOnly -and $hasSameLocationCandidate) {
        throw "ProductionRouteOnly requires a remote social route candidate, but a same-location talk candidate is already available"
    }

    if ($ProductionPursuitOnly) {
        dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
            --root $routeCandidateLoopRoot `
            --backend-url $backendUrl `
            --bridge-snapshot-url $snapshotUrl `
            --executor-url $executorUrl `
            --no-manifest `
            --run-id $RunId `
            --save-isolation-path $savesPath `
            --max-attempts 32 `
            --sleep-ms 0 `
            --skip-training `
            --use-daily-plan `
            --daily-plan-max-candidates 1 `
            --daily-plan-candidate-options "social.talk_npc" `
            --after-snapshot-wait-ms 1000 `
            --continue-after-blocked-queue-items `
            --stop-after-social-objective-complete
        if ($LASTEXITCODE -ne 0) { throw "Production social pursuit LiveTrainingLoop returned exit code $LASTEXITCODE" }

        $pursuitEvidence = Verify-ProductionSocialPursuitArtifacts `
            -LoopRoot $routeCandidateLoopRoot `
            -RunId $RunId `
            -RunDirectory $runDirectory
        Write-JsonFile (Join-Path $runDirectory "production-pursuit-verification.json") $pursuitEvidence
        $pursuitSummary = [ordered]@{
            status = "passed"
            run_id = $RunId
            save_slot = $SaveSlot
            production_social_pursuit_verified = $pursuitEvidence.Verified
            npc_name = $pursuitEvidence.NpcName
            connector_steps_applied = $pursuitEvidence.RouteStepsApplied
            wait_steps_applied = $pursuitEvidence.WaitStepsApplied
            social_interactions_applied = $pursuitEvidence.SocialInteractionsApplied
            iterations = $pursuitEvidence.Iterations
            final_location = $pursuitEvidence.FinalLocation
            future_schedule_projection = "not_used"
            scope = "same_objective_multi_connector_social_pursuit_with_live_gate_deferral"
            artifacts_dir = $runDirectory
        }
        Write-JsonFile (Join-Path $runDirectory "summary.json") $pursuitSummary
        $pursuitSummary | ConvertTo-Json -Depth 32
        return
    }

    if (-not $hasSameLocationCandidate) {
        dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
            --root $routeCandidateLoopRoot `
            --backend-url $backendUrl `
            --bridge-snapshot-url $snapshotUrl `
            --executor-url $executorUrl `
            --no-manifest `
            --run-id $RunId `
            --save-isolation-path $savesPath `
            --iterations 1 `
            --train-every 1 `
            --sleep-ms 0 `
            --use-daily-plan `
            --daily-plan-max-candidates 1 `
            --daily-plan-candidate-options "social.talk_npc" `
            --after-snapshot-wait-ms 1000 `
            --continue-after-blocked-queue-items
        if ($LASTEXITCODE -ne 0) { throw "Production social route LiveTrainingLoop returned exit code $LASTEXITCODE" }

        $productionRouteEvidence = Verify-ProductionSocialRouteStepArtifacts `
            -LoopRoot $routeCandidateLoopRoot `
            -RunId $RunId `
            -RunDirectory $runDirectory `
            -BeforeSnapshot $before
        Write-JsonFile (Join-Path $runDirectory "production-route-verification.json") $productionRouteEvidence
        $before = $productionRouteEvidence.AfterSnapshot
        $location = Get-SnapshotString $before "player" "location_id"

        if ($ProductionRouteOnly) {
            $routeOnlySummary = [ordered]@{
                status = "passed"
                run_id = $RunId
                save_slot = $SaveSlot
                production_route_candidate_verified = $productionRouteEvidence.Verified
                npc_name = $productionRouteEvidence.NpcName
                before_location = $productionRouteEvidence.BeforeLocation
                after_location = $productionRouteEvidence.NextLocation
                final_target_location = $productionRouteEvidence.FinalTargetLocation
                remaining_connector_count = $productionRouteEvidence.RemainingConnectorCount
                future_schedule_projection = "not_used"
                scope = "one_production_social_route_connector_then_fresh_snapshot"
                full_multi_connector_pursuit_verified = $false
                artifacts_dir = $runDirectory
            }
            Write-JsonFile (Join-Path $runDirectory "summary.json") $routeOnlySummary
            $routeOnlySummary | ConvertTo-Json -Depth 32
            return
        }

        $socialInteractionField = Read-FieldValue $before "npcs" "social_interaction"
        $targetNpc = $null
        $consideredNpcs = @()
        if ($null -ne $socialInteractionField -and $socialInteractionField -is [array]) {
            $ordinaryNpcs = @($socialInteractionField | Where-Object {
                -not [string]::IsNullOrWhiteSpace([string]$_.name) -and
                -not [string]::IsNullOrWhiteSpace([string]$_.location_id) -and
                $_.master_data_present -eq $true -and
                $_.current_instance_loaded -eq $true -and
                $_.is_villager -eq $true -and
                $_.vanilla_social_query_supported -eq $true -and
                $_.can_socialize_complete -eq $true -and
                $_.can_socialize -eq $true -and
                $_.is_sleeping -ne $true -and
                $_.is_invisible -ne $true
            } | Sort-Object -Property name)
            $playerLoc = [string](Read-FieldValue $before "player" "location_id")
            $routeGraphRaw = $before.state.locations.route_graph
            $reachableLocations = @{}
            if ($null -ne $routeGraphRaw -and $null -ne $routeGraphRaw.value) {
                $graphValue = $routeGraphRaw.value
                $rawEdges = @()
                if ($graphValue -is [array]) { $rawEdges = $graphValue }
                elseif ($graphValue.edges -is [array]) { $rawEdges = $graphValue.edges }
                $adjacency = @{}
                foreach ($edge in $rawEdges) {
                    if ($edge.resolved -ne $true) { continue }
                    $src = [string]$edge.from_location
                    $dst = [string]$edge.target_location
                    if (-not $adjacency.ContainsKey($src)) { $adjacency[$src] = @{} }
                    if (-not $adjacency[$src].ContainsKey($dst)) { $adjacency[$src][$dst] = $true }
                }
                if (-not [string]::IsNullOrWhiteSpace($playerLoc) -and $adjacency.ContainsKey($playerLoc)) {
                    $bfsQueue = New-Object System.Collections.Queue
                    $bfsQueue.Enqueue($playerLoc)
                    $reachableLocations[$playerLoc] = $true
                    while ($bfsQueue.Count -gt 0) {
                        $loc = [string]$bfsQueue.Dequeue()
                        if (-not $adjacency.ContainsKey($loc)) { continue }
                        foreach ($next in $adjacency[$loc].Keys) {
                            if (-not $reachableLocations.ContainsKey($next)) {
                                $reachableLocations[$next] = $true
                                $bfsQueue.Enqueue($next)
                            }
                        }
                    }
                }
            }
            foreach ($npc in $ordinaryNpcs) {
                $npcLoc = [string]$npc.location_id
                $playerLocMatch = (-not [string]::IsNullOrWhiteSpace($npcLoc) -and -not [string]::IsNullOrWhiteSpace($playerLoc) -and $npcLoc -eq $playerLoc)
                $routeReachable = $playerLocMatch -or $reachableLocations.ContainsKey($npcLoc)
                $consideredNpcs += [PSCustomObject]@{
                    name = $npc.name
                    location_id = $npcLoc
                    player_location = $playerLoc
                    player_location_match = $playerLocMatch
                    route_reachable = $routeReachable
                    master_data_present = $npc.master_data_present
                    current_instance_loaded = $npc.current_instance_loaded
                    is_villager = $npc.is_villager
                    vanilla_social_query_supported = $npc.vanilla_social_query_supported
                    can_socialize_complete = $npc.can_socialize_complete
                    can_socialize = $npc.can_socialize
                    is_sleeping = $npc.is_sleeping
                    is_invisible = $npc.is_invisible
                }
                if ($targetNpc -eq $null -and $routeReachable) {
                    $targetNpc = $npc
                }
            }
            Write-JsonFile (Join-Path $runDirectory "npcs-considered-for-route.json") ([ordered]@{ count = $consideredNpcs.Count; npcs = $consideredNpcs })
        }
        if ($null -ne $targetNpc) {
            $bfsResult = Invoke-RouteGraphBfsToNpc -Snapshot $before -NpcName $targetNpc.name `
                -ExecutorUrl $executorUrl -SavesPath $savesPath -SnapshotUrl $snapshotUrl `
                -RunDirectory $runDirectory -RunId $RunId

            Start-Sleep -Seconds 5
            Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
            $rawBfsSnapshot = Invoke-RawJsonGet -Url $snapshotUrl
            $afterBfsSnapshot = $rawBfsSnapshot | ConvertFrom-Json
            Set-Content -LiteralPath (Join-Path $runDirectory "raw-snapshot-after-bfs.json") -Value $rawBfsSnapshot -Encoding utf8
            $bfsIngestResult = Invoke-RawJsonPostStrict -Url "$backendUrl/api/v1/snapshots" -Json $rawBfsSnapshot -ErrorArtifactPath (Join-Path $runDirectory "snapshot-bfs-ingest-error.json")
            Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-bfs-ingested.json") $bfsIngestResult
        }
        else {
            $bfsFailed = [ordered]@{
                status = "failed_closed"
                reason = "No available NPC found in social_interaction for route traversal"
            }
            Write-JsonFile (Join-Path $runDirectory "route-graph-bfs-no-npc.json") $bfsFailed
            throw "No available same-location social talk candidate and no NPC found for route traversal"
        }
    }

    $talkRunId = "$RunId"
    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $talkLoopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorUrl `
        --no-manifest `
        --run-id $talkRunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "social.talk_npc" `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items
    if ($LASTEXITCODE -ne 0) { throw "Talk LiveTrainingLoop returned exit code $LASTEXITCODE" }

    $talkArtifacts = Verify-SocialTalkLoopArtifacts -LoopRoot $talkLoopRoot -RunId $talkRunId -RunDirectory $runDirectory -BeforeSnapshot $before
    Write-JsonFile (Join-Path $runDirectory "talk-verification.json") $talkArtifacts
    $talkPassed = $talkArtifacts.HasMoveBeforeSocial -and $talkArtifacts.HasQueueMoveBeforeSocial -and $talkArtifacts.HasVerifiedExecution

    $afterTalkSnapshotPath = Join-Path $runDirectory "bridge-snapshot-after-talk.json"
    $afterTalk = $null
    if (Test-Path -LiteralPath $afterTalkSnapshotPath -PathType Leaf) {
        $afterTalk = Get-Content -LiteralPath $afterTalkSnapshotPath -Raw | ConvertFrom-Json
    }
    else {
        $afterTalk = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 30
        Write-JsonFile $afterTalkSnapshotPath $afterTalk
    }

    $activeMenu = Get-SnapshotObject $afterTalk "menus" "active_menu"
    if ($null -eq $activeMenu) { throw "active_menu missing or null after talk - fail closed" }
    if ($null -eq $activeMenu.is_open) { throw "active_menu.is_open missing or null after talk - fail closed" }
    if ($activeMenu.is_open -isnot [bool]) { throw "active_menu.is_open must be boolean after talk - fail closed" }
    $dialogueOpen = ($activeMenu.is_open -eq $true)
    if ($dialogueOpen) {
        $closeRunId = "$RunId"
        dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
            --root (Join-Path $runDirectory "close-dialogue-loop") `
            --backend-url $backendUrl `
            --bridge-snapshot-url $snapshotUrl `
            --executor-url $executorUrl `
            --no-manifest `
            --run-id $closeRunId `
            --save-isolation-path $savesPath `
            --iterations 1 `
            --train-every 1 `
            --sleep-ms 0 `
            --use-daily-plan `
            --daily-plan-max-candidates 4 `
            --daily-plan-candidate-options "recovery.stabilize_day" `
            --after-snapshot-wait-ms 1000 `
            --continue-after-blocked-queue-items
        if ($LASTEXITCODE -ne 0) { throw "Close-dialogue LiveTrainingLoop returned exit code $LASTEXITCODE" }

        $closeVerifyReport = Join-Path $runDirectory "close-dialogue-loop\runs\$closeRunId\live-training-loop-report.json"
        if (-not (Test-Path -LiteralPath $closeVerifyReport -PathType Leaf)) { throw "Close-dialogue loop missing report" }
        $closeVerifyRanking = Join-Path $runDirectory "close-dialogue-loop\runs\$closeRunId\live-snapshots\ranking-response-0001.json"
        if (-not (Test-Path -LiteralPath $closeVerifyRanking -PathType Leaf)) { throw "Close-dialogue loop missing ranking" }
        $closeVerifyQueue = Join-Path $runDirectory "close-dialogue-loop\runs\$closeRunId\live-snapshots\compiled-queue-0001.json"
        if (-not (Test-Path -LiteralPath $closeVerifyQueue -PathType Leaf)) { throw "Close-dialogue loop missing compiled queue" }
        $closeVerifyExecution = Join-Path $runDirectory "close-dialogue-loop\runs\$closeRunId\live-snapshots\execution-0001.json"
        if (-not (Test-Path -LiteralPath $closeVerifyExecution -PathType Leaf)) { throw "Close-dialogue loop missing execution" }
        $closeVerifyEpisode = Join-Path $runDirectory "close-dialogue-loop\runs\$closeRunId\live-snapshots\plan-execution-episode-0001.json"
        if (-not (Test-Path -LiteralPath $closeVerifyEpisode -PathType Leaf)) { throw "Close-dialogue loop missing episode" }

        Copy-Item -LiteralPath $closeVerifyReport -Destination (Join-Path $runDirectory "close-dialogue-report.json") -Force
        Copy-Item -LiteralPath $closeVerifyRanking -Destination (Join-Path $runDirectory "close-dialogue-ranking.json") -Force
        Copy-Item -LiteralPath $closeVerifyQueue -Destination (Join-Path $runDirectory "close-dialogue-queue.json") -Force
        Copy-Item -LiteralPath $closeVerifyExecution -Destination (Join-Path $runDirectory "close-dialogue-execution.json") -Force
        Copy-Item -LiteralPath $closeVerifyEpisode -Destination (Join-Path $runDirectory "close-dialogue-episode.json") -Force

        $closeQueue = Get-Content -LiteralPath $closeVerifyQueue -Raw | ConvertFrom-Json
        $closeQueueItems = @($closeQueue.items)
        $closeOptionId = "executor.close_menu"
        $closeMenuQueueItem = @($closeQueueItems | Where-Object { $_.option_id -eq $closeOptionId })
        if ($closeMenuQueueItem.Count -ne 1) {
            throw "Close-dialogue queue must contain exactly 1 close_menu item, found $($closeMenuQueueItem.Count)"
        }
        if ($closeMenuQueueItem[0].status -ne "pending") {
            throw "Close-dialogue queue close_menu item status must be pending, got $($closeMenuQueueItem[0].status)"
        }

        $closeExecution = Get-Content -LiteralPath $closeVerifyExecution -Raw | ConvertFrom-Json
        $closeResults = @($closeExecution.step_results)
        $closeMenuResult = @($closeResults | Where-Object { $_.option_id -eq $closeOptionId })
        if ($closeMenuResult.Count -ne 1) {
            throw "Close-dialogue execution must contain exactly 1 close_menu result, found $($closeMenuResult.Count)"
        }
        $closeResult = $closeMenuResult[0]
        if ($closeResult.status -ne "applied") {
            throw "Close-dialogue close_menu result status must be applied, got $($closeResult.status). Block reasons: $($closeResult.block_reasons -join ', ')"
        }
        if ($closeResult.primitive_verification_status -ne "verified") {
            throw "Close-dialogue close_menu result verification must be verified, got $($closeResult.primitive_verification_status)"
        }

        $closeEpisode = Get-Content -LiteralPath $closeVerifyEpisode -Raw | ConvertFrom-Json
        if ($null -eq $closeEpisode.episode_id) {
            throw "Close-dialogue episode missing episode_id"
        }

        $afterCloseSnapshot = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 30
        $afterCloseActiveMenu = Get-SnapshotObject $afterCloseSnapshot "menus" "active_menu"
        if ($null -eq $afterCloseActiveMenu) { throw "active_menu missing or null after close - fail closed" }
        if ($null -eq $afterCloseActiveMenu.is_open) { throw "active_menu.is_open missing or null after close - fail closed" }
        if ($afterCloseActiveMenu.is_open -isnot [bool]) { throw "active_menu.is_open must be boolean after close - fail closed" }
        $afterCloseMenuOpen = ($afterCloseActiveMenu.is_open -eq $true)
        if ($afterCloseMenuOpen) {
            throw "Active menu still open after close-dialogue: is_open=$($afterCloseActiveMenu.is_open) type=$($afterCloseActiveMenu.type)"
        }

        Write-JsonFile (Join-Path $runDirectory "close-dialogue-verification.json") ([ordered]@{
            queue_close_menu_present = ($closeMenuQueueItem.Count -eq 1)
            queue_close_menu_pending = ($closeMenuQueueItem.Count -eq 1 -and $closeMenuQueueItem[0].status -eq "pending")
            execution_close_menu_applied = ($closeResult.status -eq "applied")
            execution_close_menu_verified = ($closeResult.primitive_verification_status -eq "verified")
            episode_present = ($null -ne $closeEpisode.episode_id)
            dialogue_closed_before_gift = (-not $afterCloseMenuOpen)
            press_attempts = $closeResult.dialogue_press_attempts
            advance_ticks = $closeResult.dialogue_advance_ticks
            dialogue_native_handled = $closeResult.dialogue_native_handled
            menu_type_before = $closeResult.dialogue_menu_type_before
            menu_type_after = $closeResult.dialogue_menu_type_after
            is_question_before = $closeResult.dialogue_is_question_before
            is_question_after = $closeResult.dialogue_is_question_after
            response_count_before = $closeResult.dialogue_response_count_before
            response_count_after = $closeResult.dialogue_response_count_after
            speaker_name_before = $closeResult.dialogue_speaker_name_before
            speaker_name_after = $closeResult.dialogue_speaker_name_after
            event_up_before = $closeResult.dialogue_event_up_before
            event_up_after = $closeResult.dialogue_event_up_after
        })
        if ($closeResult.dialogue_native_handled -ne $true) {
            throw "dialogue_native_handled must be true, got $($closeResult.dialogue_native_handled)"
        }
        if ($closeResult.dialogue_press_attempts -le 0) {
            throw "dialogue_press_attempts must be positive, got $($closeResult.dialogue_press_attempts)"
        }
        if ($closeResult.dialogue_advance_ticks -le 0) {
            throw "dialogue_advance_ticks must be positive, got $($closeResult.dialogue_advance_ticks)"
        }
        if ($closeResult.dialogue_is_question_before -ne $false) {
            throw "dialogue_is_question_before must be false, got $($closeResult.dialogue_is_question_before)"
        }
        if ($closeResult.dialogue_response_count_before -ne 0) {
            throw "dialogue_response_count_before must be 0, got $($closeResult.dialogue_response_count_before)"
        }
        if ($closeResult.dialogue_event_up_before -ne $false) {
            throw "dialogue_event_up_before must be false, got $($closeResult.dialogue_event_up_before)"
        }
        if ([string]::IsNullOrWhiteSpace($closeResult.dialogue_speaker_name_before)) {
            throw "dialogue_speaker_name_before must be nonempty, got '$($closeResult.dialogue_speaker_name_before)'"
        }
        if ($closeResult.dialogue_menu_type_after -ne "none") {
            throw "dialogue_menu_type_after must be none, got $($closeResult.dialogue_menu_type_after)"
        }
    }

    $giftRunId = "$RunId"
    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $giftLoopRoot `
        --backend-url $backendUrl `
        --bridge-snapshot-url $snapshotUrl `
        --executor-url $executorUrl `
        --no-manifest `
        --run-id $giftRunId `
        --save-isolation-path $savesPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "social.gift_npc" `
        --after-snapshot-wait-ms 1000 `
        --continue-after-blocked-queue-items

    $giftPassed = $false
    $giftVerification = $null
    if ($LASTEXITCODE -eq 0) {
        $giftArtifacts = Verify-SocialGiftLoopArtifacts -LoopRoot $giftLoopRoot -RunId $giftRunId -RunDirectory $runDirectory
        Write-JsonFile (Join-Path $runDirectory "gift-verification.json") $giftArtifacts
        $giftPassed = $giftArtifacts.HasMoveBeforeSocial -and $giftArtifacts.HasQueueMoveBeforeSocial -and $giftArtifacts.HasVerifiedExecution
        $giftVerification = $giftArtifacts
    }
    else {
        $giftBlock = [ordered]@{
            status = "failed"
            reason = "Gift LiveTrainingLoop returned exit code $LASTEXITCODE - no legal gift candidate or execution failed"
        }
        $giftVerification = $giftBlock
        Write-JsonFile (Join-Path $runDirectory "gift-failed.json") $giftBlock
        throw "Gift LiveTrainingLoop returned exit code $LASTEXITCODE - full talk+gift smoke requires both phases"
    }

    if (-not $talkPassed) { throw "Talk phase failed - full talk+gift smoke requires both phases" }
    if (-not $giftPassed) { throw "Gift phase failed - full talk+gift smoke requires both phases" }
    $overallStatus = "passed"

    $afterGift = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 30
    Write-JsonFile (Join-Path $runDirectory "bridge-snapshot-after-gift.json") $afterGift

    $giftStackDecreased = $false
    if ($null -ne $giftVerification.GiftEvidence) {
        $ge = $giftVerification.GiftEvidence
        $sb = $ge.gift_stack_before
        $sa = $ge.gift_stack_after
        if ($null -ne $sb) {
            $sbInt = [int]$sb
            if ($sbInt -gt 1 -and $null -ne $sa -and [int]$sa -eq ($sbInt - 1)) { $giftStackDecreased = $true }
            elseif ($sbInt -eq 1 -and $null -eq $sa) { $giftStackDecreased = $true }
        }
    }

    $summary = [ordered]@{
        status = $overallStatus
        run_id = $RunId
        save_slot = $SaveSlot
        talk_passed = $talkPassed
        talk_has_ranked_candidate = $talkArtifacts.HasRankedCandidate
        talk_has_move_before_social = $talkArtifacts.HasMoveBeforeSocial
        talk_has_queue_move_before_social = $talkArtifacts.HasQueueMoveBeforeSocial
        talk_has_verified_execution = $talkArtifacts.HasVerifiedExecution
        talk_evidence = $talkArtifacts.TalkEvidence
        gift_passed = $giftPassed
        gift_has_ranked_candidate = $giftVerification.HasRankedCandidate
        gift_has_move_before_social = $giftVerification.HasMoveBeforeSocial
        gift_has_queue_move_before_social = $giftVerification.HasQueueMoveBeforeSocial
        gift_has_verified_execution = $giftVerification.HasVerifiedExecution
        gift_evidence = $giftVerification.GiftEvidence
        gift_stack_decreased_by_one = $giftStackDecreased
        production_route_candidate_verified = ($null -ne $productionRouteEvidence -and $productionRouteEvidence.Verified -eq $true)
        production_route_npc_name = if ($null -eq $productionRouteEvidence) { "" } else { $productionRouteEvidence.NpcName }
        production_route_before_location = if ($null -eq $productionRouteEvidence) { "" } else { $productionRouteEvidence.BeforeLocation }
        production_route_after_location = if ($null -eq $productionRouteEvidence) { "" } else { $productionRouteEvidence.NextLocation }
        production_route_final_target_location = if ($null -eq $productionRouteEvidence) { "" } else { $productionRouteEvidence.FinalTargetLocation }
        production_route_remaining_connector_count = if ($null -eq $productionRouteEvidence) { 0 } else { $productionRouteEvidence.RemainingConnectorCount }
        before_location = $initialLocation
        after_talk_location = Get-SnapshotString $afterTalk "player" "location_id"
        after_gift_location = Get-SnapshotString $afterGift "player" "location_id"
        before_state_hash = $before.state_hash
        after_talk_state_hash = $afterTalk.state_hash
        after_gift_state_hash = $afterGift.state_hash
        backend_process_id = $backendProcess.Id
        game_process_id = $gameProcess.Id
        executor_health = $executorHealth
        artifacts_dir = $runDirectory
        kept_game_running = [bool]$KeepGameRunning
    }

    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32

    if ($overallStatus -ne "passed" -and -not $KeepGameRunning) {
        throw "Native social runtime smoke failed."
    }
}
catch {
    $errorSummary = [ordered]@{
        status = "error"
        run_id = $RunId
        error = $_.Exception.Message
        error_type = $_.Exception.GetType().Name
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    }
    Write-JsonFile (Join-Path $runDirectory "error-summary.json") $errorSummary
    throw
}
finally {
    foreach ($entry in $previousEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item -Path ("env:" + $entry.Key) -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path ("env:" + $entry.Key) -Value $entry.Value
        }
    }

    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
