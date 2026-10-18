function Get-RuntimeCropInventoryQuantity {
    param(
        [Parameter(Mandatory)] $Snapshot,
        [Parameter(Mandatory)] [string] $QualifiedItemId
    )

    $inventory = @(Read-StateValue $Snapshot "player" "inventory")
    return [int](($inventory | Where-Object {
        -not [bool]$_.is_empty -and
        [string]$_.qualified_item_id -eq $QualifiedItemId
    } | Measure-Object -Property stack -Sum).Sum)
}

function ConvertTo-RuntimeCropPurchaseParameters {
    param([Parameter(Mandatory)] $Candidate)

    $values = @($Candidate.parameters | Where-Object {
        [string]$_.name -like "continuation.*"
    } | Group-Object -Property name | ForEach-Object {
        $distinct = @($_.Group | ForEach-Object {
            [string]$_.value
        } | Select-Object -Unique)
        if ($distinct.Count -ne 1) {
            throw "Crop seed purchase continuation parameter is ambiguous: $($_.Name)"
        }
        [ordered]@{ name = [string]$_.Name; value = $distinct[0] }
    })
    foreach ($required in @(
        "continuation.option_id",
        "continuation.shop_id",
        "continuation.qualified_item_id",
        "continuation.stock_id",
        "continuation.output_stack_per_purchase",
        "continuation.output_quality",
        "continuation.max_unit_price",
        "continuation.quantity"
    )) {
        if (@($values | Where-Object { [string]$_.name -eq $required }).Count -ne 1) {
            throw "Crop seed purchase candidate omitted $required."
        }
    }
    return $values
}

function Invoke-RuntimeCropSeedAcquisition {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] $Acquisition,
        [Parameter(Mandatory)] [string] $Name
    )

    $qualifiedItemId = [string]$Acquisition.seed_qualified_item_id
    $shopId = [string]$Acquisition.shop_id
    $maxUnitPrice = [int]$Acquisition.max_unit_price
    $requiredQuantity = [int]$Acquisition.required_quantity
    $deadlineTotalDay = [int]$Acquisition.deadline_total_day
    if ([string]::IsNullOrWhiteSpace($qualifiedItemId) -or
        [string]::IsNullOrWhiteSpace($shopId) -or $maxUnitPrice -le 0 -or
        $requiredQuantity -le 0 -or $deadlineTotalDay -lt 0 -or
        [string]$Acquisition.execution_policy -ne
            "economy.buy_supplies_one_unit_fresh_snapshot_slices") {
        throw "Crop seed acquisition $Name is invalid."
    }

    $sourcePath = [IO.Path]::GetFullPath($SourceSnapshotPath)
    $source = Import-RuntimeSnapshotFile -Path $sourcePath `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
    $sourceTotalDay = [int](Read-StateValue $source.Value "time" "total_days")
    if ($sourceTotalDay -gt $deadlineTotalDay) {
        throw "Crop seed acquisition $Name missed its deadline."
    }
    $initialQuantity = Get-RuntimeCropInventoryQuantity `
        -Snapshot $source.Value -QualifiedItemId $qualifiedItemId
    $targetQuantity = [Convert]::ToInt32(
        [int64]$initialQuantity + [int64]$requiredQuantity)
    $currentPath = $source.Path
    $continuationParameters = @()
    $executions = [Collections.Generic.List[object]]::new()
    $purchaseSliceCount = 0
    $maxStages = [Math]::Min(1024, 24 + $requiredQuantity * 8)

    for ($stage = 1; $stage -le $maxStages; $stage++) {
        $current = Import-RuntimeSnapshotFile -Path $currentPath `
            -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
        $currentTotalDay = [int](Read-StateValue `
            $current.Value "time" "total_days")
        if ($currentTotalDay -ne $sourceTotalDay -or
            $currentTotalDay -gt $deadlineTotalDay) {
            throw "Crop seed acquisition $Name crossed its same-day deadline boundary."
        }
        $beforeQuantity = Get-RuntimeCropInventoryQuantity `
            -Snapshot $current.Value -QualifiedItemId $qualifiedItemId
        if ($beforeQuantity -eq $targetQuantity) {
            return [pscustomobject]@{
                SourcePath = $current.Path
                InitialQuantity = $initialQuantity
                FinalQuantity = $beforeQuantity
                RequiredQuantity = $requiredQuantity
                PurchaseSliceCount = $purchaseSliceCount
                Executions = $executions.ToArray()
            }
        }
        if ($beforeQuantity -gt $targetQuantity) {
            throw "Crop seed acquisition $Name exceeded its exact target quantity."
        }

        [object[]]$rankingParameters = if ($continuationParameters.Count -gt 0) {
            @($continuationParameters)
        }
        else {
            @(
                [ordered]@{
                    name = "continuation.option_id"
                    value = "economy.buy_supplies"
                },
                [ordered]@{
                    name = "continuation.qualified_item_id"
                    value = $qualifiedItemId
                },
                [ordered]@{
                    name = "continuation.shop_id"
                    value = $shopId
                },
                [ordered]@{
                    name = "continuation.max_unit_price"
                    value = [string]$maxUnitPrice
                },
                [ordered]@{
                    name = "continuation.quantity"
                    value = "1"
                }
            )
        }
        $stageName = "$Name-stage-$($stage.ToString('D3'))"
        $rankingPath = Join-Path $Context.ArtifactDirectory `
            "$stageName-ranking.json"
        $ranking = Invoke-RuntimeRanking -BackendUrl $BackendUrl `
            -GoalId "earn_money" `
            -SnapshotStateHash ([string]$current.Value.state_hash) `
            -OutputPath $rankingPath -OptionId "economy.buy_supplies" `
            -Parameters $rankingParameters
        $candidates = @($ranking.ranked_event_candidates | Where-Object {
            (([bool]$_.available -and
                    [string]$_.timeline_status -eq "ready_now") -or
                ([string]$_.timeline_status -eq "deferred" -and
                    [bool]$_.allowed_today -and
                    [int]$_.scheduled_wait_cost -gt 0)) -and
            [string]$_.option_id -eq "economy.buy_supplies" -and
            [string]$_.shop_id -eq $shopId -and
            [string]$_.qualified_item_id -eq $qualifiedItemId -and
            [int]$_.unit_price -le $maxUnitPrice -and
            [string]$_.kind -in @(
                "route_connector_tile",
                "interact_endpoint",
                "buy_shop_item")
        } | Sort-Object -Property rank, candidate_id)
        if ($candidates.Count -eq 0) {
            throw "Crop seed acquisition $Name has no available exact purchase stage."
        }
        $candidate = $candidates[0]
        $bound = @(ConvertTo-RuntimeCropPurchaseParameters `
            -Candidate $candidate)
        if ([string](@($bound | Where-Object {
                [string]$_.name -eq "continuation.qualified_item_id"
            })[0].value) -ne $qualifiedItemId -or
            [string](@($bound | Where-Object {
                [string]$_.name -eq "continuation.shop_id"
            })[0].value) -ne $shopId -or
            [int](@($bound | Where-Object {
                [string]$_.name -eq "continuation.max_unit_price"
            })[0].value) -gt $maxUnitPrice -or
            [string](@($bound | Where-Object {
                [string]$_.name -eq "continuation.quantity"
            })[0].value) -ne "1") {
            throw "Crop seed acquisition $Name candidate identity drifted."
        }
        $outputStack = [int](@($bound | Where-Object {
            [string]$_.name -eq "continuation.output_stack_per_purchase"
        })[0].value)
        $remaining = $targetQuantity - $beforeQuantity
        if ($outputStack -le 0 -or $outputStack -gt $remaining -or
            $remaining % $outputStack -ne 0) {
            throw "Crop seed acquisition $Name cannot satisfy its exact remaining quantity in safe slices."
        }
        $continuationParameters = $bound
        $parameterStrings = @($bound | ForEach-Object {
            "$([string]$_.name)=$([string]$_.value)"
        })
        $execution = Invoke-RuntimeDailyPlanStep -Context $Context `
            -Name $stageName -OptionId "economy.buy_supplies" `
            -CandidateKind ([string]$candidate.kind) `
            -CandidateId ([string]$candidate.candidate_id) `
            -CandidateParameters $parameterStrings `
            -SourceSnapshotPath $current.Path -AllowVerifiedContinuation
        $afterQuantity = Get-RuntimeCropInventoryQuantity `
            -Snapshot $execution.After -QualifiedItemId $qualifiedItemId
        if ($afterQuantity -lt $beforeQuantity -or
            $afterQuantity -gt $targetQuantity) {
            throw "Crop seed acquisition $Name produced an invalid inventory delta."
        }
        if ($afterQuantity -gt $beforeQuantity) {
            if ($afterQuantity - $beforeQuantity -ne $outputStack) {
                throw "Crop seed acquisition $Name purchase slice changed the wrong quantity."
            }
            $purchaseSliceCount++
            $continuationParameters = @()
        }
        elseif ([string]$execution.After.state_hash -eq
                [string]$current.Value.state_hash) {
            throw "Crop seed acquisition $Name stage made no transparent progress."
        }
        $executions.Add($execution)
        $currentPath = $execution.AfterPath
    }
    throw "Crop seed acquisition $Name exceeded its bounded stage count."
}
