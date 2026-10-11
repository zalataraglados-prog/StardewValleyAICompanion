Set-StrictMode -Version Latest

function New-ValidationImpactStringSet {
    return ,([Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal))
}

function Add-ValidationImpactValues {
    param(
        [Parameter(Mandatory)] $Set,
        [AllowEmptyCollection()] [string[]] $Values = @()
    )
    foreach ($value in $Values) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            [void]$Set.Add($value)
        }
    }
}

function ConvertTo-ValidationImpactPath {
    param([Parameter(Mandatory)] [string] $Path)

    $candidate = $Path.Trim()
    if ([string]::IsNullOrWhiteSpace($candidate) -or
        [IO.Path]::IsPathRooted($candidate)) {
        throw "Changed file must be a repository-relative path: $Path"
    }
    $candidate = $candidate.Replace('\', '/')
    while ($candidate.StartsWith("./", [StringComparison]::Ordinal)) {
        $candidate = $candidate.Substring(2)
    }
    $segments = @($candidate.Split('/') | Where-Object { $_ -ne "" })
    if ($segments -contains ".." -or
        ($segments.Count -gt 0 -and
            $segments[0].Equals(".git", [StringComparison]::OrdinalIgnoreCase))) {
        throw "Changed file escapes or targets repository metadata: $Path"
    }
    return $candidate
}

function Invoke-ValidationImpactGitNames {
    param(
        [Parameter(Mandatory)] [string] $ProjectRoot,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    $output = @(& git -C $ProjectRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return @($output | ForEach-Object { [string]$_ })
}

function Get-ValidationImpactChangedFiles {
    param(
        [Parameter(Mandatory)] [string] $ProjectRoot,
        [string] $BaseRef = "",
        [string] $HeadRef = "HEAD",
        [string[]] $ChangedFile = @(),
        [string] $ChangedFilesPath = ""
    )

    if (($ChangedFile.Count -gt 0 -or
            -not [string]::IsNullOrWhiteSpace($ChangedFilesPath)) -and
        -not [string]::IsNullOrWhiteSpace($BaseRef)) {
        throw "Explicit changed files or lists cannot be combined with a git range."
    }
    $files = @($ChangedFile)
    $source = "explicit_files"
    if (-not [string]::IsNullOrWhiteSpace($ChangedFilesPath)) {
        $listPath = if ([IO.Path]::IsPathRooted($ChangedFilesPath)) {
            [IO.Path]::GetFullPath($ChangedFilesPath)
        } else {
            [IO.Path]::GetFullPath((Join-Path $ProjectRoot $ChangedFilesPath))
        }
        if (-not (Test-Path -LiteralPath $listPath -PathType Leaf)) {
            throw "Changed-files list is missing: $listPath"
        }
        $files += @(Get-Content -LiteralPath $listPath)
        $source = if ($ChangedFile.Count -gt 0) {
            "explicit_files_and_list"
        } else {
            "explicit_file_list"
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($BaseRef)) {
        $files = Invoke-ValidationImpactGitNames -ProjectRoot $ProjectRoot `
            -Arguments @(
                "diff", "--name-only", "--diff-filter=ACMR",
                "$BaseRef...$HeadRef", "--")
        $source = "git_range"
    } elseif ($files.Count -eq 0) {
        $files = @(
            Invoke-ValidationImpactGitNames -ProjectRoot $ProjectRoot `
                -Arguments @("diff", "--name-only", "--diff-filter=ACMR", "--")
            Invoke-ValidationImpactGitNames -ProjectRoot $ProjectRoot `
                -Arguments @("diff", "--cached", "--name-only", "--diff-filter=ACMR", "--")
        )
        $source = "git_worktree"
    }

    return [pscustomobject]@{
        Files = @($files | Where-Object {
                -not [string]::IsNullOrWhiteSpace([string]$_)
            } | ForEach-Object {
                ConvertTo-ValidationImpactPath -Path ([string]$_)
            } | Sort-Object -Unique)
        Source = $source
        BaseRef = $BaseRef
        HeadRef = if ([string]::IsNullOrWhiteSpace($BaseRef)) {
            ""
        } else {
            $HeadRef
        }
    }
}

function New-ValidationImpactState {
    return [pscustomobject]@{
        Domains = New-ValidationImpactStringSet
        Builds = New-ValidationImpactStringSet
        Tests = New-ValidationImpactStringSet
        Guards = New-ValidationImpactStringSet
        Native = New-ValidationImpactStringSet
        Rules = New-ValidationImpactStringSet
        Reasoning = [Collections.Generic.List[object]]::new()
        Uncertain = $false
    }
}

function Add-ValidationImpact {
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)] [string] $File,
        [Parameter(Mandatory)] [string] $Rule,
        [Parameter(Mandatory)] [string] $Reason,
        [string[]] $Domains = @(),
        [string[]] $Builds = @(),
        [string[]] $Tests = @(),
        [string[]] $Guards = @(),
        [string[]] $Native = @(),
        [switch] $Uncertain
    )

    Add-ValidationImpactValues -Set $State.Domains -Values $Domains
    Add-ValidationImpactValues -Set $State.Builds -Values $Builds
    Add-ValidationImpactValues -Set $State.Tests -Values $Tests
    Add-ValidationImpactValues -Set $State.Guards -Values $Guards
    Add-ValidationImpactValues -Set $State.Native -Values $Native
    [void]$State.Rules.Add($Rule)
    $State.Reasoning.Add([ordered]@{
        changed_file = $File
        rule_id = $Rule
        reason = $Reason
    })
    if ($Uncertain) {
        $State.Uncertain = $true
    }
}

function Get-ValidationImpactNativeCandidates {
    param([Parameter(Mandatory)] [string] $Path)

    $result = @()
    if ($Path -match '(?i)fish|bobber|crabpot|crab_pot') {
        $result += "representative_fishing_native_replay"
    }
    if ($Path -match '(?i)mining|mine|monster|drop|combat|skull|volcano|quarry') {
        $result += "representative_mining_native_replay"
    }
    if ($Path -match '(?i)machine|materialtransfer|material_transfer') {
        $result += "representative_machine_native_replay"
    }
    if ($Path -match '(?i)shop|purchase|economy') {
        $result += "representative_shop_native_replay"
    }
    if ($Path -match '(?i)social|friendship|npc') {
        $result += "representative_social_native_replay"
    }
    if ($Path -match '(?i)fullshipment|full_shipment') {
        $result += "full_shipment_changed_chain_replay"
    }
    return @($result | Sort-Object -Unique)
}

function Add-ValidationImpactBroadQuality {
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)] [string] $File,
        [Parameter(Mandatory)] [string] $Rule,
        [Parameter(Mandatory)] [string] $Reason
    )

    Add-ValidationImpact -State $State -File $File -Rule $Rule `
        -Reason $Reason -Uncertain `
        -Domains @("broad_quality", "core", "backend", "bootstrap", "training_tooling") `
        -Builds @(
            "dotnet build src/StardewAI.Core/StardewAI.Core.csproj -c Release --no-restore",
            "dotnet build tools/StardewAI.LiveTrainingLoop/StardewAI.LiveTrainingLoop.csproj -c Release --no-restore",
            "dotnet build tools/StardewAI.KnowledgeCompiler/StardewAI.KnowledgeCompiler.csproj -c Release --no-restore",
            "dotnet build experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-restore") `
        -Tests @(
            "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-bootstrap-hermetic",
            "dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj -c Release --no-restore -p:GameFreeGovernance=true",
            "dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj -c Release") `
        -Guards @(
            "powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-CurrentWorkCheckpoint.ps1 -Check",
            "git diff --check") `
        -Native @("manual_impact_review_before_native_replay")
}

function Add-ValidationImpactForFile {
    param(
        [Parameter(Mandatory)] $State,
        [Parameter(Mandatory)] [string] $File
    )

    $native = Get-ValidationImpactNativeCandidates -Path $File
    if ($File -match '^(docs/|README(?:\.|$)|[^/]+\.md$)') {
        Add-ValidationImpact -State $State -File $File -Rule "docs_only" `
            -Reason "Documentation changes need formatting and generated-checkpoint checks only." `
            -Domains @("documentation") `
            -Guards @(
                "powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Update-CurrentWorkCheckpoint.ps1 -Check",
                "git diff --check")
        return
    }
    if ($File.StartsWith("tests/StardewAI.Core.Tests/", [StringComparison]::Ordinal)) {
        $testName = [IO.Path]::GetFileNameWithoutExtension($File).Split('.')[0]
        Add-ValidationImpact -State $State -File $File -Rule "core_test" `
            -Reason "A Core test change should first run its own class under the game-free profile." `
            -Domains @("core_tests") `
            -Builds @("dotnet build src/StardewAI.Core/StardewAI.Core.csproj -c Release --no-restore") `
            -Tests @("dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true --filter FullyQualifiedName~$testName") `
            -Guards @("git diff --check") -Native $native
        return
    }
    if ($File.StartsWith("tests/StardewAI.Backend.Tests/", [StringComparison]::Ordinal)) {
        $testName = [IO.Path]::GetFileNameWithoutExtension($File).Split('.')[0]
        Add-ValidationImpact -State $State -File $File -Rule "backend_test" `
            -Reason "A Backend test change should first run its own class." `
            -Domains @("backend_tests") `
            -Tests @("dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj --filter FullyQualifiedName~$testName") `
            -Guards @("git diff --check")
        return
    }
    if ($File.StartsWith("src/StardewAI.Contracts/", [StringComparison]::Ordinal) -or
        $File.StartsWith("src/StardewAI.RuntimePrimitives/", [StringComparison]::Ordinal)) {
        Add-ValidationImpact -State $State -File $File -Rule "shared_contract" `
            -Reason "Shared contracts fan out to Core, Backend, Bootstrap, and training consumers." `
            -Domains @("contracts", "core", "backend", "bootstrap") `
            -Builds @(
                "dotnet build src/StardewAI.Core/StardewAI.Core.csproj -c Release --no-restore",
                "dotnet build src/StardewAI.Backend/StardewAI.Backend.csproj -c Release --no-restore",
                "dotnet build experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-restore") `
            -Tests @(
                "dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true",
                "dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj",
                "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-bootstrap-hermetic") `
            -Guards @("git diff --check") -Native $native
        return
    }
    if ($File.StartsWith("src/StardewAI.Core/", [StringComparison]::Ordinal)) {
        $runtimeBoundary = $File -match '/(Execution|OptionRegistry|Planning|Integration)/'
        $coreNative = @($native)
        if ($runtimeBoundary -and $coreNative.Count -eq 0) {
            $coreNative += "representative_core_runtime_transition_replay"
        }
        Add-ValidationImpact -State $State -File $File -Rule "core_shared" `
            -Reason "Core changes require Core/Backend/Bootstrap compile compatibility and game-free contracts." `
            -Domains @("core", "backend", "bootstrap") `
            -Builds @(
                "dotnet build src/StardewAI.Core/StardewAI.Core.csproj -c Release --no-restore",
                "dotnet build src/StardewAI.Backend/StardewAI.Backend.csproj -c Release --no-restore",
                "dotnet build experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-restore") `
            -Tests @(
                "dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true",
                "dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj",
                "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-bootstrap-hermetic") `
            -Guards @("git diff --check") -Native $coreNative
        return
    }
    if ($File.StartsWith("src/StardewAI.Backend/", [StringComparison]::Ordinal)) {
        Add-ValidationImpact -State $State -File $File -Rule "backend" `
            -Reason "Backend changes require the Backend build and tests." `
            -Domains @("backend") `
            -Builds @("dotnet build src/StardewAI.Backend/StardewAI.Backend.csproj -c Release --no-restore") `
            -Tests @("dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj") `
            -Guards @("git diff --check") -Native $native
        return
    }
    if ($File.StartsWith("src/StardewAI.TransparentBridge/Adapters/", [StringComparison]::Ordinal)) {
        $bridgeNative = @($native)
        if ($bridgeNative.Count -eq 0) {
            $bridgeNative += "representative_transparent_bridge_native_replay"
        }
        Add-ValidationImpact -State $State -File $File -Rule "transparent_bridge_adapter" `
            -Reason "Adapter semantics require the Bridge build, source guards, and a representative native replay recommendation." `
            -Domains @("transparent_bridge") `
            -Builds @("dotnet build src/StardewAI.TransparentBridge/StardewAI.TransparentBridge.csproj -c Release --no-restore -p:GamePath=<GAME_PATH>") `
            -Tests @("dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true --filter FullyQualifiedName~SourceGuard") `
            -Guards @("git diff --check") -Native $bridgeNative
        return
    }
    if ($File.StartsWith("experiments/StardewAI.GoalConditionedBootstrap/", [StringComparison]::Ordinal)) {
        $bootstrapTests = @(
            "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-bootstrap-hermetic")
        if ($File -match '(Acquisition|TargetDate|Portfolio|SupportingTransition)') {
            $bootstrapTests += "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-acquisition-route-dispatch"
        }
        if ($File -match 'FullShipment') {
            $bootstrapTests += "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-full-shipment-static-compilability --output-root artifacts/fast-check/full-shipment"
        }
        if ($File -match '(GoalMethod|TeacherFrontier|StageOneCollection)') {
            $bootstrapTests += "dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-current-stage-one-collection --output-root artifacts/fast-check/stage-one"
        }
        Add-ValidationImpact -State $State -File $File -Rule "goal_conditioned_bootstrap" `
            -Reason "Bootstrap domain changes require a clean Bootstrap build plus hermetic and domain self-tests." `
            -Domains @("bootstrap") `
            -Builds @("dotnet build experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-restore") `
            -Tests $bootstrapTests -Guards @("git diff --check") -Native $native
        return
    }
    if ($File.StartsWith("tools/StardewAI.RuntimeTestHarness/", [StringComparison]::Ordinal)) {
        $harnessNative = @($native)
        if ($harnessNative.Count -eq 0) {
            $harnessNative += "representative_runtime_executor_replay"
        }
        Add-ValidationImpact -State $State -File $File -Rule "runtime_test_harness" `
            -Reason "Runtime executor/verifier changes require the harness build, source guards, and a native replay recommendation." `
            -Domains @("runtime_test_harness", "runtime_executor") `
            -Builds @("dotnet build tools/StardewAI.RuntimeTestHarness/StardewAI.RuntimeTestHarness.csproj -c Release --no-restore -p:GamePath=<GAME_PATH>") `
            -Tests @("dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true --filter FullyQualifiedName~RuntimeHarness") `
            -Guards @("git diff --check") -Native $harnessNative
        return
    }
    if ($File.StartsWith("tools/StardewAI.LiveTrainingLoop/", [StringComparison]::Ordinal) -or
        $File.StartsWith("tools/StardewAI.ProductExecutor/", [StringComparison]::Ordinal) -or
        $File.StartsWith("tools/StardewAI.Policy", [StringComparison]::Ordinal)) {
        $project = $File.Split('/')[1]
        Add-ValidationImpact -State $State -File $File -Rule "training_or_product_tool" `
            -Reason "Training/product tooling changes require the owning project and Core/Backend contract checks." `
            -Domains @("training_tooling", "core", "backend") `
            -Builds @(
                "dotnet build tools/$project/$project.csproj -c Release --no-restore",
                "dotnet build src/StardewAI.Core/StardewAI.Core.csproj -c Release --no-restore") `
            -Tests @(
                "dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj --no-restore -p:GameFreeGovernance=true",
                "dotnet test tests/StardewAI.Backend.Tests/StardewAI.Backend.Tests.csproj") `
            -Guards @("git diff --check") -Native $native
        return
    }
    if ($File.StartsWith("scripts/lib/", [StringComparison]::Ordinal)) {
        Add-ValidationImpactBroadQuality -State $State -File $File `
            -Rule "shared_runtime_script" `
            -Reason "Shared runtime script fanout is broad; first feedback must fall back to hosted-quality-equivalent checks."
        [void]$State.Guards.Add("powershell_ast:$File")
        return
    }
    if ($File.StartsWith("scripts/", [StringComparison]::Ordinal)) {
        $scriptNative = @($native)
        if ($File -match '^scripts/Invoke-Runtime' -and $scriptNative.Count -eq 0) {
            $scriptNative += "representative_changed_runtime_script_replay"
        }
        Add-ValidationImpact -State $State -File $File -Rule "script_tooling" `
            -Reason "PowerShell changes require AST validation; runtime scripts only recommend their representative replay." `
            -Domains @("script_tooling") `
            -Guards @("powershell_ast:$File", "git diff --check") `
            -Native $scriptNative
        return
    }
    if ($File -match '^(catalogs/|data/|knowledge-artifacts\.lock\.json$)') {
        Add-ValidationImpact -State $State -File $File -Rule "versioned_authority" `
            -Reason "Versioned authority changes require compiler/bootstrap rebuilds and drift preflight." `
            -Domains @("authority", "knowledge_compiler", "bootstrap") `
            -Builds @(
                "dotnet build tools/StardewAI.KnowledgeCompiler/StardewAI.KnowledgeCompiler.csproj -c Release --no-restore",
                "dotnet build experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-restore") `
            -Tests @("dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build -- self-test-bootstrap-hermetic") `
            -Guards @("git diff --check") `
            -Native @("authority_drift_preflight_before_native_replay")
        return
    }
    if ($File -match '^(\.github/|Directory\.|global\.json$|NuGet\.|StardewValleyAICompanion\.sln$)' -or
        $File.EndsWith(".csproj", [StringComparison]::OrdinalIgnoreCase) -or
        $File.EndsWith("packages.lock.json", [StringComparison]::OrdinalIgnoreCase)) {
        Add-ValidationImpactBroadQuality -State $State -File $File `
            -Rule "repository_shared_configuration" `
            -Reason "Repository/build configuration can affect every hosted-quality stage."
        return
    }

    Add-ValidationImpactBroadQuality -State $State -File $File `
        -Rule "unknown_impact" `
        -Reason "No conservative rule owns this path; broad fallback is mandatory."
}

function New-ValidationImpactAdvisory {
    param(
        [AllowEmptyCollection()] [string[]] $ChangedFiles = @(),
        [Parameter(Mandatory)] [string] $ChangeSource,
        [string] $BaseRef = "",
        [string] $HeadRef = ""
    )

    $state = New-ValidationImpactState
    foreach ($file in @($ChangedFiles | Sort-Object -Unique)) {
        Add-ValidationImpactForFile -State $state -File $file
    }
    return [ordered]@{
        schema_version = "stardewai.validation_impact_advisory.v1"
        status = if ($ChangedFiles.Count -eq 0) {
            "no_changes"
        } else {
            "advisory_ready"
        }
        advisory_only = $true
        change_source = $ChangeSource
        base_ref = $BaseRef
        head_ref = $HeadRef
        changed_files = @($ChangedFiles | Sort-Object -Unique)
        impacted_domains = @($state.Domains | Sort-Object)
        matched_rules = @($state.Rules | Sort-Object)
        recommended_builds = @($state.Builds | Sort-Object)
        recommended_test_filters = @($state.Tests | Sort-Object)
        recommended_source_guards = @($state.Guards | Sort-Object)
        native_replay_candidates = @($state.Native | Sort-Object)
        uncertain_impact = [bool]$state.Uncertain
        reasoning = @($state.Reasoning)
        full_quality_still_required = $true
        hosted_quality_workflow = ".github/workflows/quality.yml"
        evidence_reuse_authorized = $false
        native_replay_execution_authorized = $false
    }
}
